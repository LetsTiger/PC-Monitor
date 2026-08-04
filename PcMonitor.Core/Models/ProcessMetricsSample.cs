using PcMonitor.Core.Metrics;

namespace PcMonitor.Core.Models;

public sealed record ProcessMetricsSample(
    DateTimeOffset Timestamp,

    int ProcessId,
    string ProcessName,
    string? ExecutablePath,
    bool IsForegroundProcess,

    double CpuUsagePercent,

    long WorkingSetBytes,
    long PrivateMemoryBytes,

    int ThreadCount,
    int HandleCount,

    double? ReadBytesPerSecond,
    double? WriteBytesPerSecond) : IMetricSample;