namespace PcMonitor.Core.Models;

public sealed record PerformanceTrendPoint(
    Guid RunId,
    string RunName,
    DateTimeOffset StartedAt,
    double Score,
    double? AverageFps,
    double? OnePercentLowFps,
    double? P99FrameTimeMilliseconds,
    double? MaximumGpuTemperatureCelsius,
    int StutterEpisodeCount,
    BottleneckType Bottleneck);
