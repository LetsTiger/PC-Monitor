namespace PcMonitor.Core.Models;

public sealed record FrameChartPoint(
    DateTimeOffset Timestamp,
    double SecondsFromStart,
    double FramesPerSecond,
    double FrameTimeMilliseconds,
    bool IsSpike,
    double? CpuUsagePercent,
    double? MaximumCpuThreadLoadPercent,
    double? GpuLoadPercent,
    double? MemoryUsagePercent,
    double? VramUsagePercent,
    double? DiskActivityPercent,
    double? DiskReadMegabytesPerSecond,
    bool? GatewaySuccess,
    bool? InternetSuccess,
    string? BackgroundProcessName,
    double? BackgroundProcessCpuPercent);
