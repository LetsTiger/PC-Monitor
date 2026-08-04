using System.ComponentModel;
using System.Diagnostics;
using PcMonitor.Core.Collectors;
using PcMonitor.Core.Metrics;
using PcMonitor.Core.Models;

namespace PcMonitor.Collectors.Windows;

public sealed class ProcessMetricsCollector :
    IMetricCollector,
    IAsyncDisposable
{
    private readonly TimeSpan _interval;
    private readonly int _maximumProcessesPerSample;
    private readonly object _stateLock = new();

    private readonly Dictionary<int, ProcessSnapshot>
        _previousSnapshots = [];

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _collectionTask;

    public ProcessMetricsCollector(
        TimeSpan? interval = null,
        int maximumProcessesPerSample = 8)
    {
        _interval =
            interval ?? TimeSpan.FromSeconds(1);

        if (_interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(interval));
        }

        if (maximumProcessesPerSample <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumProcessesPerSample));
        }

        _maximumProcessesPerSample =
            maximumProcessesPerSample;
    }

    public string Name =>
        "Windows Process Metrics";

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

            _previousSnapshots.Clear();

            _cancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            IsRunning = true;

            CancellationToken collectionToken =
                _cancellationTokenSource.Token;

            // Die gesamte Prozessabfrage läuft im Hintergrund.
            // Insbesondere die erste Momentaufnahme darf nicht
            // synchron im WPF-UI-Thread ausgeführt werden.
            _collectionTask =
                Task.Run(
                    () => CollectLoopAsync(collectionToken),
                    CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        Task? collectionTask;
        CancellationTokenSource? cancellationTokenSource;

        lock (_stateLock)
        {
            if (!IsRunning &&
                _collectionTask is null)
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
                // Reguläres Stoppen.
            }
        }

        cancellationTokenSource?.Dispose();
        _previousSnapshots.Clear();
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
            // Warm-up-Messung im Hintergrund.
            Dictionary<int, ProcessSnapshot> initialSnapshots =
                CaptureSnapshots();

            ReplacePreviousSnapshots(
                initialSnapshots);

            using var timer =
                new PeriodicTimer(_interval);

            while (await timer.WaitForNextTickAsync(
                       cancellationToken))
            {
                try
                {
                    CollectSamples();
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // Ein einzelner fehlerhafter Messzyklus
                    // darf den gesamten Run nicht beenden.
                }
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
        DateTimeOffset timestamp =
            DateTimeOffset.Now;

        int? foregroundProcessId =
            ForegroundProcessReader.TryGetProcessId();

        Dictionary<int, ProcessSnapshot> currentSnapshots =
            CaptureSnapshots();

        var calculatedSamples =
            new List<CalculatedProcessSample>();

        foreach (
            KeyValuePair<int, ProcessSnapshot> pair
            in currentSnapshots)
        {
            ProcessSnapshot current = pair.Value;

            if (!_previousSnapshots.TryGetValue(
                    pair.Key,
                    out ProcessSnapshot? previous))
            {
                continue;
            }

            double elapsedSeconds =
                (current.Timestamp - previous.Timestamp)
                .TotalSeconds;

            if (elapsedSeconds <= 0)
            {
                continue;
            }

            double cpuSeconds =
                (current.TotalProcessorTime -
                 previous.TotalProcessorTime)
                .TotalSeconds;

            double cpuUsagePercent =
                cpuSeconds /
                elapsedSeconds /
                Environment.ProcessorCount *
                100.0;

            cpuUsagePercent =
                Math.Clamp(
                    cpuUsagePercent,
                    0,
                    100);

            double? readBytesPerSecond =
                CalculateRate(
                    current.TotalReadBytes,
                    previous.TotalReadBytes,
                    elapsedSeconds);

            double? writeBytesPerSecond =
                CalculateRate(
                    current.TotalWriteBytes,
                    previous.TotalWriteBytes,
                    elapsedSeconds);

            calculatedSamples.Add(
                new CalculatedProcessSample(
                    Snapshot: current,
                    CpuUsagePercent:
                        cpuUsagePercent,
                    ReadBytesPerSecond:
                        readBytesPerSecond,
                    WriteBytesPerSecond:
                        writeBytesPerSecond,
                    IsForegroundProcess:
                        current.ProcessId ==
                        foregroundProcessId));
        }

        List<CalculatedProcessSample> selectedSamples =
            SelectRelevantProcesses(
                calculatedSamples);

        foreach (CalculatedProcessSample selected
                 in selectedSamples)
        {
            ProcessSnapshot snapshot =
                selected.Snapshot;

            var sample =
                new ProcessMetricsSample(
                    Timestamp: timestamp,

                    ProcessId:
                        snapshot.ProcessId,

                    ProcessName:
                        snapshot.ProcessName,

                    ExecutablePath:
                        snapshot.ExecutablePath,

                    IsForegroundProcess:
                        selected.IsForegroundProcess,

                    CpuUsagePercent:
                        selected.CpuUsagePercent,

                    WorkingSetBytes:
                        snapshot.WorkingSetBytes,

                    PrivateMemoryBytes:
                        snapshot.PrivateMemoryBytes,

                    ThreadCount:
                        snapshot.ThreadCount,

                    HandleCount:
                        snapshot.HandleCount,

                    ReadBytesPerSecond:
                        selected.ReadBytesPerSecond,

                    WriteBytesPerSecond:
                        selected.WriteBytesPerSecond);

            SampleCollected?.Invoke(
                this,
                sample);
        }

        ReplacePreviousSnapshots(
            currentSnapshots);
    }
    
    private void ReplacePreviousSnapshots(
        IReadOnlyDictionary<int, ProcessSnapshot> snapshots)
    {
        _previousSnapshots.Clear();

        foreach (
            KeyValuePair<int, ProcessSnapshot> pair
            in snapshots)
        {
            _previousSnapshots[pair.Key] =
                pair.Value;
        }
    }

    private List<CalculatedProcessSample>
        SelectRelevantProcesses(
            IReadOnlyCollection<CalculatedProcessSample>
                calculatedSamples)
    {
        List<CalculatedProcessSample> selected =
            calculatedSamples
                .OrderByDescending(sample =>
                    sample.CpuUsagePercent)
                .ThenByDescending(sample =>
                    sample.Snapshot.WorkingSetBytes)
                .Take(_maximumProcessesPerSample)
                .ToList();

        CalculatedProcessSample? foreground =
            calculatedSamples
                .FirstOrDefault(sample =>
                    sample.IsForegroundProcess);

        if (foreground is not null &&
            selected.All(sample =>
                sample.Snapshot.ProcessId !=
                foreground.Snapshot.ProcessId))
        {
            selected.Add(foreground);
        }

        return selected;
    }

    private Dictionary<int, ProcessSnapshot>
        CaptureSnapshots()
    {
        var snapshots =
            new Dictionary<int, ProcessSnapshot>();

        Process[] processes =
            Process.GetProcesses();

        foreach (Process process in processes)
        {
            using (process)
            {
                ProcessSnapshot? snapshot =
                    TryCreateSnapshot(process);

                if (snapshot is not null)
                {
                    snapshots[snapshot.ProcessId] =
                        snapshot;
                }
            }
        }

        return snapshots;
    }

    private static ProcessSnapshot? TryCreateSnapshot(
        Process process)
    {
        try
        {
            process.Refresh();

            int processId =
                process.Id;

            string processName =
                process.ProcessName;

            string? executablePath = null;

            TimeSpan totalProcessorTime =
                process.TotalProcessorTime;

            long workingSetBytes =
                process.WorkingSet64;

            long privateMemoryBytes =
                process.PrivateMemorySize64;

            int threadCount =
                TryGetThreadCount(process);

            int handleCount =
                TryGetHandleCount(process);

            ulong? totalReadBytes = null;
            ulong? totalWriteBytes = null;

            if (ProcessIoReader.TryRead(
                    process,
                    out ulong readBytes,
                    out ulong writeBytes))
            {
                totalReadBytes = readBytes;
                totalWriteBytes = writeBytes;
            }

            return new ProcessSnapshot(
                ProcessId: processId,
                ProcessName: processName,
                ExecutablePath: executablePath,
                TotalProcessorTime: totalProcessorTime,
                WorkingSetBytes: workingSetBytes,
                PrivateMemoryBytes: privateMemoryBytes,
                ThreadCount: threadCount,
                HandleCount: handleCount,
                TotalReadBytes: totalReadBytes,
                TotalWriteBytes: totalWriteBytes,
                Timestamp: DateTimeOffset.Now);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (Win32Exception)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static string? TryGetExecutablePath(
        Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private static double? CalculateRate(
        ulong? currentValue,
        ulong? previousValue,
        double elapsedSeconds)
    {
        if (currentValue is null ||
            previousValue is null ||
            currentValue < previousValue ||
            elapsedSeconds <= 0)
        {
            return null;
        }

        ulong difference =
            currentValue.Value -
            previousValue.Value;

        return difference /
               elapsedSeconds;
    }
    
    private static int TryGetThreadCount(
        Process process)
    {
        try
        {
            return process.Threads.Count;
        }
        catch
        {
            return 0;
        }
    }

    private static int TryGetHandleCount(
        Process process)
    {
        try
        {
            return process.HandleCount;
        }
        catch
        {
            return 0;
        }
    }

    private sealed record CalculatedProcessSample(
        ProcessSnapshot Snapshot,
        double CpuUsagePercent,
        double? ReadBytesPerSecond,
        double? WriteBytesPerSecond,
        bool IsForegroundProcess);
}