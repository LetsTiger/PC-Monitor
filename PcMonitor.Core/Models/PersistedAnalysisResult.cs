namespace PcMonitor.Core.Models;
public sealed record PersistedAnalysisResult(
 Guid RunId, string AnalysisVersion, string RuleSetVersion,
 DateTimeOffset CreatedAt, string GameProfileId,
 string MainApplicationName, int? MainProcessId,
 string FrameApplication, int? FrameProcessId,
 AnalysisQualityReport Quality, PerformanceScoreResult? PerformanceScore,
 BottleneckAnalysis? Bottleneck,
 IReadOnlyList<PerformanceRecommendation> Recommendations);
