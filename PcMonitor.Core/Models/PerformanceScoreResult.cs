namespace PcMonitor.Core.Models;

public sealed record PerformanceScoreResult(
    Guid RunId,
    double TotalScore,
    string Grade,
    double FpsScore,
    double LowFpsScore,
    double FrameTimeScore,
    double StabilityScore,
    double HardwareScore,
    double NetworkScore,
    string Summary);
