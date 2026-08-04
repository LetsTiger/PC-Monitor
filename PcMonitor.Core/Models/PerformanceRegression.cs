namespace PcMonitor.Core.Models;

public sealed record PerformanceRegression(
    bool HasBaseline,
    int BaselineRunCount,
    double? CurrentScore,
    double? BaselineScore,
    double? ScoreDifference,
    double? CurrentAverageFps,
    double? BaselineAverageFps,
    double? AverageFpsDifferencePercent,
    double? CurrentOnePercentLowFps,
    double? BaselineOnePercentLowFps,
    double? OnePercentLowDifferencePercent,
    string Status,
    string Explanation);
