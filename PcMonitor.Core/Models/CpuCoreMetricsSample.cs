using PcMonitor.Core.Metrics;

namespace PcMonitor.Core.Models;

public sealed record CpuCoreMetricsSample(
    DateTimeOffset Timestamp,
    int CoreIndex,
    int ThreadIndex,
    string SensorName,
    double LoadPercent) : IMetricSample;