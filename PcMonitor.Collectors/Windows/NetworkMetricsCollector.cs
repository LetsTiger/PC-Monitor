using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using PcMonitor.Core.Collectors;
using PcMonitor.Core.Metrics;
using PcMonitor.Core.Models;

namespace PcMonitor.Collectors.Windows;

public sealed class NetworkMetricsCollector :
    IMetricCollector,
    IAsyncDisposable
{
    private const int MeasurementWindowSize = 60;

    private readonly TimeSpan _interval;
    private readonly TimeSpan _pingTimeout;
    private readonly string _internetTarget;
    private readonly object _stateLock = new();

    private readonly Queue<PingMeasurement>
        _gatewayMeasurements = new();

    private readonly Queue<PingMeasurement>
        _internetMeasurements = new();

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _collectionTask;

    private int _gatewayConsecutiveFailures;
    private int _internetConsecutiveFailures;

    public NetworkMetricsCollector(
        string internetTarget = "1.1.1.1",
        TimeSpan? interval = null,
        TimeSpan? pingTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(internetTarget))
        {
            throw new ArgumentException(
                "Das Internetziel darf nicht leer sein.",
                nameof(internetTarget));
        }

        _internetTarget = internetTarget.Trim();
        _interval = interval ?? TimeSpan.FromSeconds(1);
        _pingTimeout =
            pingTimeout ?? TimeSpan.FromMilliseconds(800);

        if (_interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(interval),
                "Das Messintervall muss größer als null sein.");
        }

        if (_pingTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pingTimeout),
                "Das Ping-Timeout muss größer als null sein.");
        }
    }

    public string Name => "Windows Network Metrics";

    public bool IsRunning { get; private set; }

    public event EventHandler<IMetricSample>? SampleCollected;

    public Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (IsRunning)
            {
                return Task.CompletedTask;
            }

            ResetStatistics();

            _cancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            IsRunning = true;

            _collectionTask = CollectLoopAsync(
                _cancellationTokenSource.Token);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        Task? collectionTask;
        CancellationTokenSource? cancellationTokenSource;

        lock (_stateLock)
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;

            collectionTask = _collectionTask;
            cancellationTokenSource =
                _cancellationTokenSource;

            _collectionTask = null;
            _cancellationTokenSource = null;
        }

        cancellationTokenSource?.Cancel();

        if (collectionTask is not null)
        {
            try
            {
                await collectionTask;
            }
            catch (OperationCanceledException)
            {
                // Reguläres Stoppen des Collectors.
            }
        }

        cancellationTokenSource?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private async Task CollectLoopAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var timer =
                new PeriodicTimer(_interval);

            while (await timer.WaitForNextTickAsync(
                       cancellationToken))
            {
                string? gatewayAddress =
                    FindDefaultGatewayAddress();

                Task<PingMeasurement> gatewayTask =
                    gatewayAddress is null
                        ? Task.FromResult(
                            PingMeasurement.Failed)
                        : MeasurePingAsync(
                            gatewayAddress,
                            cancellationToken);

                Task<PingMeasurement> internetTask =
                    MeasurePingAsync(
                        _internetTarget,
                        cancellationToken);

                await Task.WhenAll(
                    gatewayTask,
                    internetTask);

                PingMeasurement gatewayMeasurement =
                    await gatewayTask;

                PingMeasurement internetMeasurement =
                    await internetTask;

                AddMeasurement(
                    _gatewayMeasurements,
                    gatewayMeasurement);

                AddMeasurement(
                    _internetMeasurements,
                    internetMeasurement);

                _gatewayConsecutiveFailures =
                    gatewayMeasurement.Success
                        ? 0
                        : _gatewayConsecutiveFailures + 1;

                _internetConsecutiveFailures =
                    internetMeasurement.Success
                        ? 0
                        : _internetConsecutiveFailures + 1;

                NetworkStatistics gatewayStatistics =
                    CalculateStatistics(
                        _gatewayMeasurements);

                NetworkStatistics internetStatistics =
                    CalculateStatistics(
                        _internetMeasurements);

                var sample =
                    new NetworkMetricsSample(
                        Timestamp: DateTimeOffset.Now,

                        GatewayAddress: gatewayAddress,
                        GatewaySuccess:
                            gatewayMeasurement.Success,
                        GatewayLatencyMilliseconds:
                            gatewayMeasurement.LatencyMilliseconds,
                        GatewayPacketLossPercent:
                            gatewayStatistics.PacketLossPercent,
                        GatewayJitterMilliseconds:
                            gatewayStatistics.JitterMilliseconds,
                        GatewayConsecutiveFailures:
                            _gatewayConsecutiveFailures,

                        InternetTarget: _internetTarget,
                        InternetSuccess:
                            internetMeasurement.Success,
                        InternetLatencyMilliseconds:
                            internetMeasurement.LatencyMilliseconds,
                        InternetPacketLossPercent:
                            internetStatistics.PacketLossPercent,
                        InternetJitterMilliseconds:
                            internetStatistics.JitterMilliseconds,
                        InternetConsecutiveFailures:
                            _internetConsecutiveFailures);

                SampleCollected?.Invoke(
                    this,
                    sample);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Reguläres Stoppen des Collectors.
        }
        finally
        {
            lock (_stateLock)
            {
                IsRunning = false;
            }
        }
    }

    private async Task<PingMeasurement> MeasurePingAsync(
        string target,
        CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();

            byte[] buffer = new byte[32];

            var options = new PingOptions
            {
                DontFragment = true,
                Ttl = 128
            };

            PingReply reply =
                await ping.SendPingAsync(
                    target,
                    _pingTimeout,
                    buffer,
                    options,
                    cancellationToken);

            if (reply.Status != IPStatus.Success)
            {
                return PingMeasurement.Failed;
            }

            return new PingMeasurement(
                Success: true,
                LatencyMilliseconds:
                    reply.RoundtripTime);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PingException)
        {
            return PingMeasurement.Failed;
        }
        catch (SocketException)
        {
            return PingMeasurement.Failed;
        }
    }

    private static void AddMeasurement(
        Queue<PingMeasurement> measurements,
        PingMeasurement measurement)
    {
        measurements.Enqueue(measurement);

        while (measurements.Count >
               MeasurementWindowSize)
        {
            measurements.Dequeue();
        }
    }

    private static NetworkStatistics CalculateStatistics(
        IEnumerable<PingMeasurement> measurements)
    {
        PingMeasurement[] values =
            measurements.ToArray();

        if (values.Length == 0)
        {
            return new NetworkStatistics(
                PacketLossPercent: 0,
                JitterMilliseconds: null);
        }

        int failedCount =
            values.Count(value => !value.Success);

        double packetLossPercent =
            failedCount /
            (double)values.Length *
            100.0;

        long[] successfulLatencies =
            values
                .Where(value =>
                    value.Success &&
                    value.LatencyMilliseconds is not null)
                .Select(value =>
                    value.LatencyMilliseconds!.Value)
                .ToArray();

        double? jitter =
            CalculateJitter(
                successfulLatencies);

        return new NetworkStatistics(
            PacketLossPercent:
                packetLossPercent,
            JitterMilliseconds:
                jitter);
    }

    private static double? CalculateJitter(
        IReadOnlyList<long> latencies)
    {
        if (latencies.Count < 2)
        {
            return null;
        }

        double totalDifference = 0;

        for (int index = 1;
             index < latencies.Count;
             index++)
        {
            totalDifference += Math.Abs(
                latencies[index] -
                latencies[index - 1]);
        }

        return totalDifference /
               (latencies.Count - 1);
    }

    private void ResetStatistics()
    {
        _gatewayMeasurements.Clear();
        _internetMeasurements.Clear();

        _gatewayConsecutiveFailures = 0;
        _internetConsecutiveFailures = 0;
    }

    private static string? FindDefaultGatewayAddress()
    {
        IEnumerable<NetworkInterface> interfaces =
            NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(networkInterface =>
                    networkInterface.OperationalStatus ==
                    OperationalStatus.Up)
                .Where(networkInterface =>
                    networkInterface.NetworkInterfaceType !=
                    NetworkInterfaceType.Loopback)
                .Where(networkInterface =>
                    networkInterface.NetworkInterfaceType !=
                    NetworkInterfaceType.Tunnel);

        foreach (NetworkInterface networkInterface
                 in interfaces)
        {
            IPInterfaceProperties properties;

            try
            {
                properties =
                    networkInterface.GetIPProperties();
            }
            catch (NetworkInformationException)
            {
                continue;
            }

            GatewayIPAddressInformation? gateway =
                properties
                    .GatewayAddresses
                    .FirstOrDefault(address =>
                        address.Address.AddressFamily ==
                            AddressFamily.InterNetwork &&
                        !address.Address.Equals(
                            IPAddress.Any) &&
                        !address.Address.Equals(
                            IPAddress.None));

            if (gateway is not null)
            {
                return gateway.Address.ToString();
            }
        }

        return null;
    }

    private readonly record struct PingMeasurement(
        bool Success,
        long? LatencyMilliseconds)
    {
        public static PingMeasurement Failed =>
            new(
                Success: false,
                LatencyMilliseconds: null);
    }

    private readonly record struct NetworkStatistics(
        double PacketLossPercent,
        double? JitterMilliseconds);
}