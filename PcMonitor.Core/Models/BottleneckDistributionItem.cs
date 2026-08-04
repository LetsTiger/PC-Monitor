namespace PcMonitor.Core.Models;

public sealed record BottleneckDistributionItem(
    BottleneckType Type,
    int EpisodeCount,
    double SharePercent,
    double WeightedDurationSeconds);
