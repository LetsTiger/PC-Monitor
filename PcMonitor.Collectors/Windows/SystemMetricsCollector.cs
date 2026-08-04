using System.Runtime.InteropServices;
using PcMonitor.Core.Collectors;
using PcMonitor.Core.Metrics;
using PcMonitor.Core.Models;

namespace PcMonitor.Collectors.Windows;

public sealed class SystemMetricsCollector :
    IMetricCollector,
    IAsyncDisposable
{
    private readonly TimeSpan _interval;
    private readonly object _stateLock = new();

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _collectionTask;

    private ulong _previousIdleTime;
    private ulong _previousKernelTime;
    private ulong _previousUserTime;
    private bool _hasPreviousCpuSample;

    public SystemMetricsCollector()
        : this(TimeSpan.FromSeconds(1))
    {
    }

    public SystemMetricsCollector(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(interval),
                "Das Messintervall muss größer als null sein.");
        }

        _interval = interval;
    }

    public string Name => "Windows System Metrics";

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

            _hasPreviousCpuSample = false;

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
            cancellationTokenSource = _cancellationTokenSource;

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
            InitializeCpuMeasurement();

            using var timer = new PeriodicTimer(_interval);

            while (await timer.WaitForNextTickAsync(
                       cancellationToken))
            {
                double cpuUsage = ReadCpuUsagePercent();
                MemoryData memoryData = ReadMemoryData();

                var sample = new SystemMetricsSample(
                    Timestamp: DateTimeOffset.Now,
                    CpuUsagePercent: cpuUsage,
                    MemoryUsagePercent: memoryData.UsagePercent,
                    UsedMemoryBytes: memoryData.UsedBytes,
                    TotalMemoryBytes: memoryData.TotalBytes);

                SampleCollected?.Invoke(this, sample);
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

    private void InitializeCpuMeasurement()
    {
        if (!TryReadSystemTimes(
                out ulong idleTime,
                out ulong kernelTime,
                out ulong userTime))
        {
            throw new InvalidOperationException(
                "Die CPU-Zeiten konnten nicht gelesen werden.");
        }

        _previousIdleTime = idleTime;
        _previousKernelTime = kernelTime;
        _previousUserTime = userTime;
        _hasPreviousCpuSample = true;
    }

    private double ReadCpuUsagePercent()
    {
        if (!TryReadSystemTimes(
                out ulong idleTime,
                out ulong kernelTime,
                out ulong userTime))
        {
            return 0;
        }

        if (!_hasPreviousCpuSample)
        {
            _previousIdleTime = idleTime;
            _previousKernelTime = kernelTime;
            _previousUserTime = userTime;
            _hasPreviousCpuSample = true;

            return 0;
        }

        ulong idleDelta = idleTime - _previousIdleTime;
        ulong kernelDelta = kernelTime - _previousKernelTime;
        ulong userDelta = userTime - _previousUserTime;

        _previousIdleTime = idleTime;
        _previousKernelTime = kernelTime;
        _previousUserTime = userTime;

        ulong totalDelta = kernelDelta + userDelta;

        if (totalDelta == 0)
        {
            return 0;
        }

        ulong busyDelta = totalDelta >= idleDelta
            ? totalDelta - idleDelta
            : 0;

        double usagePercent =
            busyDelta / (double)totalDelta * 100.0;

        return Math.Clamp(usagePercent, 0, 100);
    }

    private static MemoryData ReadMemoryData()
    {
        var memoryStatus = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        if (!GlobalMemoryStatusEx(ref memoryStatus))
        {
            throw new InvalidOperationException(
                "Die Arbeitsspeicherdaten konnten nicht gelesen werden.");
        }

        ulong totalBytes = memoryStatus.TotalPhysical;
        ulong availableBytes = memoryStatus.AvailablePhysical;
        ulong usedBytes = totalBytes - availableBytes;

        double usagePercent = totalBytes == 0
            ? 0
            : usedBytes / (double)totalBytes * 100.0;

        return new MemoryData(
            usagePercent,
            usedBytes,
            totalBytes);
    }

    private static bool TryReadSystemTimes(
        out ulong idleTime,
        out ulong kernelTime,
        out ulong userTime)
    {
        bool success = GetSystemTimes(
            out FileTime idle,
            out FileTime kernel,
            out FileTime user);

        idleTime = ToUnsignedLong(idle);
        kernelTime = ToUnsignedLong(kernel);
        userTime = ToUnsignedLong(user);

        return success;
    }

    private static ulong ToUnsignedLong(FileTime fileTime)
    {
        return ((ulong)fileTime.HighDateTime << 32)
               | fileTime.LowDateTime;
    }

    private readonly record struct MemoryData(
        double UsagePercent,
        ulong UsedBytes,
        ulong TotalBytes);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out FileTime idleTime,
        out FileTime kernelTime,
        out FileTime userTime);

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Auto,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(
        ref MemoryStatusEx buffer);
}