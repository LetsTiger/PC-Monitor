namespace PcMonitor.Core.Models;
public sealed record AnalysisQualityReport(
 Guid RunId, AnalysisQualityLevel Level, double ScorePercent,
 string Summary, IReadOnlyList<string> Issues,
 long SystemSamples, long HardwareSamples, long ProcessSamples,
 long FrameSamples, double? FrameCoverageSeconds, double? MaximumFrameGapSeconds);
