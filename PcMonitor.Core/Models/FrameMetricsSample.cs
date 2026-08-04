using PcMonitor.Core.Metrics;

namespace PcMonitor.Core.Models;

public sealed record FrameMetricsSample(
    DateTimeOffset Timestamp,
    string Application,
    int ProcessId,
    string? PresentRuntime,
    string? PresentMode,
    double FrameTimeMilliseconds,
    double FramesPerSecond,
    double? CpuBusyMilliseconds,
    double? GpuTimeMilliseconds,
    double? DisplayLatencyMilliseconds) : IMetricSample;