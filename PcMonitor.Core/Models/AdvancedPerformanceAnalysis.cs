namespace PcMonitor.Core.Models;

public sealed record AdvancedPerformanceAnalysis(
    GameProfile Profile,
    PerformanceScoreResult Score,
    IReadOnlyList<PerformanceRecommendation> Recommendations,
    IReadOnlyList<TimelineStory> TimelineStories,
    IReadOnlyList<PerformanceHeatmapCell> Heatmap,
    PerformanceRegression Regression,
    GameSessionStatistics SessionStatistics,
    IReadOnlyList<PerformanceTrendPoint> Trends);
