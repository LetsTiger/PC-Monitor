namespace PcMonitor.Core.Models;

public sealed record StutterEpisode(
    Guid RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    int FrameCount,
    int SpikeCount,
    double AverageFrameTimeMilliseconds,
    double MaximumFrameTimeMilliseconds,
    double MinimumFramesPerSecond,
    BottleneckType Cause,
    double ConfidencePercent,
    string Title,
    string Description,
    double? AverageCpuUsagePercent,
    double? MaximumCpuThreadLoadPercent,
    double? AverageGpuLoadPercent,
    double? MaximumMemoryUsagePercent,
    double? MaximumVramUsagePercent,
    double? MaximumDiskActivityPercent,
    double? MaximumDiskReadMegabytesPerSecond,
    string? BackgroundProcessName,
    double? BackgroundProcessCpuPercent)
{
    public TimeSpan Duration =>
        EndedAt >= StartedAt
            ? EndedAt - StartedAt
            : TimeSpan.Zero;
}
