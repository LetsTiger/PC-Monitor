using System.Threading.Channels;
using PcMonitor.Core.Models;
using PcMonitor.Core.Storage;

namespace PcMonitor.Application.Network;

public sealed class NetworkIncidentDetector :
    INetworkIncidentDetector,
    IAsyncDisposable
{
    private readonly IMonitoringStorage _storage;
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    private Channel<NetworkMetricsSample>? _channel;
    private CancellationTokenSource? _workerCancellation;
    private Task? _workerTask;

    private Guid? _currentRunId;
    private bool _initialized;
    private bool _disposed;

    public NetworkIncidentDetector(
        IMonitoringStorage storage)
    {
        _storage = storage
            ?? throw new ArgumentNullException(
                nameof(storage));
    }

    public bool IsRunning { get; private set; }

    public async Task StartAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _stateLock.WaitAsync(cancellationToken);

        try
        {
            if (IsRunning)
            {
                throw new InvalidOperationException(
                    "Die Netzwerkereignis-Erkennung läuft bereits.");
            }

            if (!_initialized)
            {
                await _storage.InitializeAsync(
                    cancellationToken);

                _initialized = true;
            }

            _channel =
                Channel.CreateBounded<NetworkMetricsSample>(
                    new BoundedChannelOptions(2_000)
                    {
                        SingleReader = true,
                        SingleWriter = false,
                        FullMode =
                            BoundedChannelFullMode.DropOldest
                    });

            _workerCancellation =
                new CancellationTokenSource();

            _currentRunId = runId;
            IsRunning = true;

            _workerTask =
                ProcessSamplesAsync(
                    runId,
                    _channel.Reader,
                    _workerCancellation.Token);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public bool TryQueueSample(
        NetworkMetricsSample sample)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(sample);

        return IsRunning &&
               _channel is not null &&
               _channel.Writer.TryWrite(sample);
    }

    public async Task StopAsync(
        DateTimeOffset endedAt,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _stateLock.WaitAsync(cancellationToken);

        try
        {
            if (!IsRunning)
            {
                return;
            }

            if (_channel is not null)
            {
                _channel.Writer.TryComplete();
            }

            if (_workerTask is not null)
            {
                await _workerTask;
            }

            ResetState();
        }
        catch
        {
            ResetState();
            throw;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (IsRunning)
        {
            if (_channel is not null)
            {
                _channel.Writer.TryComplete();
            }

            if (_workerCancellation is not null)
            {
                await _workerCancellation.CancelAsync();
            }

            if (_workerTask is not null)
            {
                try
                {
                    await _workerTask;
                }
                catch
                {
                    // Beim Herunterfahren nicht weiterwerfen.
                }
            }
        }

        ResetState();

        _disposed = true;
        _stateLock.Dispose();
    }

    private async Task ProcessSamplesAsync(
        Guid runId,
        ChannelReader<NetworkMetricsSample> reader,
        CancellationToken cancellationToken)
    {
        OpenIncident? gatewayIncident = null;
        OpenIncident? internetIncident = null;

        DateTimeOffset? lastTimestamp = null;

        try
        {
            await foreach (
                NetworkMetricsSample sample
                in reader.ReadAllAsync(cancellationToken))
            {
                lastTimestamp = sample.Timestamp;

                gatewayIncident =
                    await ProcessTargetAsync(
                        runId,
                        NetworkIncidentTarget.Gateway,
                        sample.GatewayAddress,
                        sample.GatewaySuccess,
                        sample.Timestamp,
                        gatewayIncident,
                        cancellationToken);

                internetIncident =
                    await ProcessTargetAsync(
                        runId,
                        NetworkIncidentTarget.Internet,
                        sample.InternetTarget,
                        sample.InternetSuccess,
                        sample.Timestamp,
                        internetIncident,
                        cancellationToken);
            }

            DateTimeOffset closingTimestamp =
                lastTimestamp ?? DateTimeOffset.Now;

            if (gatewayIncident is not null)
            {
                await SaveIncidentAsync(
                    runId,
                    NetworkIncidentTarget.Gateway,
                    gatewayIncident,
                    closingTimestamp,
                    cancellationToken);
            }

            if (internetIncident is not null)
            {
                await SaveIncidentAsync(
                    runId,
                    NetworkIncidentTarget.Internet,
                    internetIncident,
                    closingTimestamp,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Regulärer Abbruch beim Programmende.
        }
    }

    private async Task<OpenIncident?> ProcessTargetAsync(
        Guid runId,
        NetworkIncidentTarget target,
        string? address,
        bool success,
        DateTimeOffset timestamp,
        OpenIncident? openIncident,
        CancellationToken cancellationToken)
    {
        if (!success)
        {
            if (openIncident is null)
            {
                return new OpenIncident(
                    Address: address,
                    StartedAt: timestamp,
                    LastFailureAt: timestamp,
                    FailureCount: 1);
            }

            return openIncident with
            {
                LastFailureAt = timestamp,
                FailureCount =
                    openIncident.FailureCount + 1
            };
        }

        if (openIncident is null)
        {
            return null;
        }

        await SaveIncidentAsync(
            runId,
            target,
            openIncident,
            timestamp,
            cancellationToken);

        return null;
    }

    private Task SaveIncidentAsync(
        Guid runId,
        NetworkIncidentTarget target,
        OpenIncident openIncident,
        DateTimeOffset endedAt,
        CancellationToken cancellationToken)
    {
        var incident =
            new NetworkIncident(
                Id: Guid.NewGuid(),
                RunId: runId,
                Target: target,
                Address: openIncident.Address,
                StartedAt: openIncident.StartedAt,
                EndedAt: endedAt,
                FailureCount: openIncident.FailureCount);

        return _storage.SaveNetworkIncidentAsync(
            incident,
            cancellationToken);
    }

    private void ResetState()
    {
        IsRunning = false;
        _currentRunId = null;

        _channel = null;
        _workerTask = null;

        _workerCancellation?.Dispose();
        _workerCancellation = null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    private sealed record OpenIncident(
        string? Address,
        DateTimeOffset StartedAt,
        DateTimeOffset LastFailureAt,
        int FailureCount);
}