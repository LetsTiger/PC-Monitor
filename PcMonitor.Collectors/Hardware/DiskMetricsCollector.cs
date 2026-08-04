using LibreHardwareMonitor.Hardware;
using PcMonitor.Core.Collectors;
using PcMonitor.Core.Metrics;
using PcMonitor.Core.Models;

namespace PcMonitor.Collectors.Hardware;

public sealed class DiskMetricsCollector :
    IMetricCollector,
    IAsyncDisposable
{
    private readonly TimeSpan _interval;
    private readonly object _stateLock = new();

    private Computer? _computer;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _collectionTask;
    private bool _isWarmedUp;

    public DiskMetricsCollector(
        TimeSpan? interval = null)
    {
        _interval =
            interval ?? TimeSpan.FromSeconds(1);

        if (_interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(interval),
                "Das Messintervall muss größer als null sein.");
        }
    }

    public string Name =>
        "Libre Hardware Monitor Disks";

    public bool IsRunning { get; private set; }

    public event EventHandler<IMetricSample>?
        SampleCollected;

    public Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (IsRunning)
            {
                return Task.CompletedTask;
            }

            _computer =
                new Computer
                {
                    IsCpuEnabled = false,
                    IsGpuEnabled = false,
                    IsMemoryEnabled = false,
                    IsMotherboardEnabled = false,
                    IsControllerEnabled = false,
                    IsNetworkEnabled = false,
                    IsStorageEnabled = true,
                    IsBatteryEnabled = false,
                    IsPsuEnabled = false
                };
            
            _isWarmedUp = false;

            _computer.Open();

            _cancellationTokenSource =
                CancellationTokenSource
                    .CreateLinkedTokenSource(
                        cancellationToken);

            IsRunning = true;

            _collectionTask =
                CollectLoopAsync(
                    _cancellationTokenSource.Token);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        Task? collectionTask;
        CancellationTokenSource? cancellationTokenSource;
        Computer? computer;

        lock (_stateLock)
        {
            if (!IsRunning &&
                _collectionTask is null &&
                _computer is null)
            {
                return;
            }

            IsRunning = false;

            collectionTask = _collectionTask;
            cancellationTokenSource =
                _cancellationTokenSource;

            computer = _computer;

            _collectionTask = null;
            _cancellationTokenSource = null;
            _computer = null;
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
                // Reguläres Stoppen.
            }
        }

        cancellationTokenSource?.Dispose();
        computer?.Close();
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
            CollectSamples();

            using var timer =
                new PeriodicTimer(_interval);

            while (await timer.WaitForNextTickAsync(
                       cancellationToken))
            {
                CollectSamples();
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Reguläres Stoppen.
        }
        finally
        {
            lock (_stateLock)
            {
                IsRunning = false;
            }
        }
    }

    private void CollectSamples()
    {
        Computer? computer = _computer;

        if (computer is null)
        {
            return;
        }

        var storageHardware =
            computer.Hardware
                .Where(hardware =>
                    hardware.HardwareType ==
                    HardwareType.Storage)
                .ToArray();

        foreach (IHardware hardware in storageHardware)
        {
            UpdateHardwareTree(hardware);
        }

        // Die erste Aktualisierung initialisiert interne Differenzwerte.
        // Durchsatz und Aktivität können dabei ungültige Spitzen enthalten.
        if (!_isWarmedUp)
        {
            _isWarmedUp = true;
            return;
        }

        DateTimeOffset timestamp =
            DateTimeOffset.Now;

        foreach (IHardware hardware in storageHardware)
        {
            DiskMetricsSample sample =
                CreateSample(
                    hardware,
                    timestamp);

            SampleCollected?.Invoke(
                this,
                sample);
        }
    }

    private static DiskMetricsSample CreateSample(
        IHardware hardware,
        DateTimeOffset timestamp)
    {
        return new DiskMetricsSample(
            Timestamp: timestamp,

            DeviceId:
            hardware.Identifier.ToString(),

            DeviceName:
            hardware.Name,

            TemperatureCelsius:
            ReadTemperature(hardware),

            ReadActivityPercent:
            ReadValue(
                hardware,
                SensorType.Load,
                "Read Activity"),

            WriteActivityPercent:
            ReadValue(
                hardware,
                SensorType.Load,
                "Write Activity"),

            TotalActivityPercent:
            ReadValue(
                hardware,
                SensorType.Load,
                "Total Activity"),

            ReadBytesPerSecond:
            ReadValue(
                hardware,
                SensorType.Throughput,
                "Read Rate"),

            WriteBytesPerSecond:
            ReadValue(
                hardware,
                SensorType.Throughput,
                "Write Rate"),

            UsedSpacePercent:
            ReadValue(
                hardware,
                SensorType.Load,
                "Used Space"),

            FreeSpaceGigabytes:
            ReadValue(
                hardware,
                SensorType.Data,
                "Free Space"),

            TotalSpaceGigabytes:
            ReadValue(
                hardware,
                SensorType.Data,
                "Total Space"),

            RemainingLifePercent:
            ReadValue(
                hardware,
                SensorType.Level,
                "Life"));
    }

    private static double? ReadTemperature(
        IHardware hardware)
    {
        double? value =
            ReadValue(
                hardware,
                SensorType.Temperature,
                "Composite Temperature");

        value ??=
            ReadValue(
                hardware,
                SensorType.Temperature,
                "Temperature");

        return value is > 0 and < 150
            ? value
            : null;
    }

    private static double? ReadValue(
        IHardware hardware,
        SensorType sensorType,
        params string[] preferredNames)
    {
        IEnumerable<ISensor> sensors =
            EnumerateSensors(hardware)
                .Where(sensor =>
                    sensor.SensorType == sensorType &&
                    sensor.Value.HasValue);

        ISensor[] sensorArray =
            sensors.ToArray();

        foreach (string preferredName in preferredNames)
        {
            ISensor? exactMatch =
                sensorArray.FirstOrDefault(sensor =>
                    string.Equals(
                        sensor.Name,
                        preferredName,
                        StringComparison.OrdinalIgnoreCase));

            if (exactMatch is not null)
            {
                return exactMatch.Value;
            }
        }

        foreach (string preferredName in preferredNames)
        {
            ISensor? partialMatch =
                sensorArray.FirstOrDefault(sensor =>
                    sensor.Name.Contains(
                        preferredName,
                        StringComparison.OrdinalIgnoreCase));

            if (partialMatch is not null)
            {
                return partialMatch.Value;
            }
        }

        return null;
    }

    private static IEnumerable<ISensor> EnumerateSensors(
        IHardware hardware)
    {
        foreach (ISensor sensor in hardware.Sensors)
        {
            yield return sensor;
        }

        foreach (IHardware subHardware
                 in hardware.SubHardware)
        {
            foreach (ISensor sensor
                     in EnumerateSensors(subHardware))
            {
                yield return sensor;
            }
        }
    }

    private static void UpdateHardwareTree(
        IHardware hardware)
    {
        hardware.Update();

        foreach (IHardware subHardware
                 in hardware.SubHardware)
        {
            UpdateHardwareTree(subHardware);
        }
    }
}