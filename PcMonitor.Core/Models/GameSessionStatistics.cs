namespace PcMonitor.Core.Models;

public sealed record GameSessionStatistics(
    string GameName,
    int RunCount,
    TimeSpan TotalDuration,
    double? AverageScore,
    double? BestScore,
    double? AverageFps,
    double? BestAverageFps,
    double? AverageOnePercentLowFps,
    double? AverageP99FrameTimeMilliseconds,
    double? AverageStutterEpisodes,
    BottleneckType MostCommonBottleneck);
