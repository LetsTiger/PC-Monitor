namespace PcMonitor.Core.Models;

public sealed record PerformanceRecommendation(
    int Priority,
    string Category,
    string Title,
    string Description,
    string ExpectedEffect);
