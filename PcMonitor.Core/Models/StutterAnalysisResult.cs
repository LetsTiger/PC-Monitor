namespace PcMonitor.Core.Models;

public sealed record StutterAnalysisResult(
    Guid RunId,
    IReadOnlyList<StutterEpisode> Episodes,
    IReadOnlyList<BottleneckDistributionItem> Distribution,
    int TotalSpikeCount,
    double TotalStutterDurationSeconds);
