namespace PcMonitor.Collectors.Windows;

internal sealed record ProcessSnapshot(
    int ProcessId,
    string ProcessName,
    string? ExecutablePath,
    TimeSpan TotalProcessorTime,
    long WorkingSetBytes,
    long PrivateMemoryBytes,
    int ThreadCount,
    int HandleCount,
    ulong? TotalReadBytes,
    ulong? TotalWriteBytes,
    DateTimeOffset Timestamp);