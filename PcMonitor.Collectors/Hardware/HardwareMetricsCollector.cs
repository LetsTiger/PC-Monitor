using LibreHardwareMonitor.Hardware;
using PcMonitor.Core.Collectors;
using PcMonitor.Core.Metrics;
using PcMonitor.Core.Models;

namespace PcMonitor.Collectors.Hardware;

public sealed class HardwareMetricsCollector :
    IMetricCollector,
    IAsyncDisposable
{
    private readonly TimeSpan _interval;
    private readonly object _stateLock = new();
    
    private bool _sensorReportWritten;

    private Computer? _computer;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _collectionTask;

    public HardwareMetricsCollector(
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
        "Libre Hardware Monitor";

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
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,

                    // Für diesen ersten Collector nicht benötigt.
                    IsMemoryEnabled = false,
                    IsMotherboardEnabled = false,
                    IsControllerEnabled = false,
                    IsNetworkEnabled = false,
                    IsStorageEnabled = false,
                    IsBatteryEnabled = false,
                    IsPsuEnabled = false
                };

            _sensorReportWritten = false;
            
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
            // Direkt eine erste Messung erzeugen.
            CollectSample();

            using var timer =
                new PeriodicTimer(_interval);

            while (await timer.WaitForNextTickAsync(
                       cancellationToken))
            {
                CollectSample();
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

    private void CollectSample()
    {
        Computer? computer = _computer;

        if (computer is null)
        {
            return;
        }

        IHardware? cpuHardware = null;
        IHardware? gpuHardware = null;

        foreach (IHardware hardware
                 in computer.Hardware)
        {
            UpdateHardwareTree(hardware);

            if (IsCpu(hardware) &&
                cpuHardware is null)
            {
                cpuHardware = hardware;
            }

            if (IsGpu(hardware) &&
                gpuHardware is null)
            {
                gpuHardware = hardware;
            }
        }
        
        if (!_sensorReportWritten)
        {
            WriteSensorReport(computer);
            _sensorReportWritten = true;
        }

        DateTimeOffset timestamp =
            DateTimeOffset.Now;

        HardwareMetricsSample sample =
            CreateSample(
                cpuHardware,
                gpuHardware,
                timestamp);

        SampleCollected?.Invoke(
            this,
            sample);

        if (cpuHardware is not null)
        {
            EmitCpuCoreSamples(
                cpuHardware,
                timestamp);
        }
    }

    private static HardwareMetricsSample CreateSample(
        IHardware? cpu,
        IHardware? gpu,
        DateTimeOffset timestamp)
    {
        return new HardwareMetricsSample(
            Timestamp: timestamp,

            CpuName: cpu?.Name,

            CpuTemperatureCelsius:
                cpu is null
                    ? null
                    : ReadCpuPackageTemperature(cpu),

            CpuMaximumCoreTemperatureCelsius:
                cpu is null
                    ? null
                    : ReadMaximumCpuCoreTemperature(cpu),

            CpuAverageClockMegahertz:
                cpu is null
                    ? null
                    : ReadAverageCpuClock(cpu),

            CpuPackagePowerWatts:
                cpu is null
                    ? null
                    : ReadCpuPackagePower(cpu),

            GpuName: gpu?.Name,

            GpuLoadPercent:
                gpu is null
                    ? null
                    : ReadGpuLoad(gpu),

            GpuTemperatureCelsius:
                gpu is null
                    ? null
                    : ReadGpuTemperature(gpu),

            GpuHotspotTemperatureCelsius:
                gpu is null
                    ? null
                    : ReadGpuHotspotTemperature(gpu),

            GpuCoreClockMegahertz:
                gpu is null
                    ? null
                    : ReadGpuCoreClock(gpu),

            GpuMemoryClockMegahertz:
                gpu is null
                    ? null
                    : ReadGpuMemoryClock(gpu),

            GpuPowerWatts:
                gpu is null
                    ? null
                    : ReadGpuPower(gpu),

            GpuUsedMemoryMegabytes:
                gpu is null
                    ? null
                    : ReadGpuUsedMemory(gpu),

            GpuTotalMemoryMegabytes:
                gpu is null
                    ? null
                    : ReadGpuTotalMemory(gpu));
    }

    private static double? ReadCpuPackageTemperature(
        IHardware cpu)
    {
        double? value =
            SensorReader.FindValue(
                cpu,
                SensorType.Temperature,
                "CPU Package",
                "Core Average",
                "Core Max");

        return value is > 0 and < 120
            ? value
            : null;
    }

    private static double? ReadMaximumCpuCoreTemperature(
        IHardware cpu)
    {
        return SensorReader.FindMaximumValue(
            cpu,
            SensorType.Temperature,
            sensor =>
                sensor.Name.StartsWith(
                    "CPU Core #",
                    StringComparison.OrdinalIgnoreCase) &&
                !sensor.Name.Contains(
                    "Distance to TjMax",
                    StringComparison.OrdinalIgnoreCase) &&
                sensor.Value is > 0 and < 120);
    }

    private static double? ReadAverageCpuClock(
        IHardware cpu)
    {
        return SensorReader.FindAverageValue(
            cpu,
            SensorType.Clock,
            sensor =>
                sensor.Name.StartsWith(
                    "CPU Core #",
                    StringComparison.OrdinalIgnoreCase) &&
                sensor.Value is > 0);
    }

    private static double? ReadCpuPackagePower(
        IHardware cpu)
    {
        double? value =
            SensorReader.FindValue(
                cpu,
                SensorType.Power,
                "CPU Package");

        return value is > 0
            ? value
            : null;
    }

    private static double? ReadGpuLoad(
        IHardware gpu)
    {
        return SensorReader.FindValue(
            gpu,
            SensorType.Load,
            "GPU Core",
            "GPU",
            "D3D 3D");
    }

    private static double? ReadGpuTemperature(
        IHardware gpu)
    {
        return SensorReader.FindValue(
            gpu,
            SensorType.Temperature,
            "GPU Core",
            "GPU Temperature",
            "GPU");
    }

    private static double? ReadGpuHotspotTemperature(
        IHardware gpu)
    {
        return SensorReader.FindValue(
            gpu,
            SensorType.Temperature,
            "GPU Hot Spot",
            "GPU Hotspot",
            "Hot Spot",
            "Hotspot");
    }

    private static double? ReadGpuCoreClock(
        IHardware gpu)
    {
        return SensorReader.FindValue(
            gpu,
            SensorType.Clock,
            "GPU Core",
            "GPU Clock",
            "Core");
    }

    private static double? ReadGpuMemoryClock(
        IHardware gpu)
    {
        return SensorReader.FindValue(
            gpu,
            SensorType.Clock,
            "GPU Memory",
            "Memory");
    }

    private static double? ReadGpuPower(
        IHardware gpu)
    {
        return SensorReader.FindValue(
            gpu,
            SensorType.Power,
            "GPU Package",
            "GPU Power",
            "Total Graphics Power",
            "Board Power",
            "Power Total");
    }

    private static double? ReadGpuUsedMemory(
        IHardware gpu)
    {
        return SensorReader.FindValue(
            gpu,
            SensorType.SmallData,
            "GPU Memory Used",
            "Memory Used",
            "D3D Dedicated Memory Used");
    }

    private static double? ReadGpuTotalMemory(
        IHardware gpu)
    {
        double? totalMemory =
            SensorReader.FindValue(
                gpu,
                SensorType.SmallData,
                "GPU Memory Total",
                "Memory Total",
                "D3D Dedicated Memory");

        if (totalMemory is not null)
        {
            return totalMemory;
        }

        double? usedMemory =
            ReadGpuUsedMemory(gpu);

        double? availableMemory =
            SensorReader.FindValue(
                gpu,
                SensorType.SmallData,
                "GPU Memory Free",
                "Memory Free",
                "GPU Memory Available");

        return usedMemory is not null &&
               availableMemory is not null
            ? usedMemory + availableMemory
            : null;
    }

    private static bool IsCpu(
        IHardware hardware)
    {
        return hardware.HardwareType ==
               HardwareType.Cpu;
    }

    private static bool IsGpu(
        IHardware hardware)
    {
        return hardware.HardwareType is
            HardwareType.GpuNvidia or
            HardwareType.GpuAmd or
            HardwareType.GpuIntel;
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
    
    private static void WriteSensorReport(
        Computer computer)
    {
        string report =
            HardwareSensorDiagnostic.CreateSensorReport(
                computer.Hardware);

        string directory =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "PcMonitor",
                "Diagnostics");

        Directory.CreateDirectory(directory);

        string filePath =
            Path.Combine(
                directory,
                "hardware-sensors.txt");

        File.WriteAllText(
            filePath,
            report);
    }
    
    private void EmitCpuCoreSamples(
        IHardware cpuHardware,
        DateTimeOffset timestamp)
    {
        foreach (ISensor sensor
                 in EnumerateSensors(cpuHardware))
        {
            if (sensor.SensorType != SensorType.Load ||
                !sensor.Value.HasValue)
            {
                continue;
            }

            if (!CpuLoadSensorParser.TryParse(
                    sensor.Name,
                    out int coreIndex,
                    out int threadIndex))
            {
                continue;
            }

            double loadPercent =
                Math.Clamp(
                    sensor.Value.Value,
                    0,
                    100);

            var sample =
                new CpuCoreMetricsSample(
                    Timestamp: timestamp,
                    CoreIndex: coreIndex,
                    ThreadIndex: threadIndex,
                    SensorName: sensor.Name,
                    LoadPercent: loadPercent);

            SampleCollected?.Invoke(
                this,
                sample);
        }
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
}