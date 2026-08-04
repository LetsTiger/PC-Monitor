using PcMonitor.Core.Metrics;

namespace PcMonitor.Core.Models;

public sealed record SystemMetricsSample(
    DateTimeOffset Timestamp,
    double CpuUsagePercent,
    double MemoryUsagePercent,
    ulong UsedMemoryBytes,
    ulong TotalMemoryBytes) : IMetricSample;