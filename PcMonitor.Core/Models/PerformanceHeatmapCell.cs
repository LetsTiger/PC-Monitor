namespace PcMonitor.Core.Models;

public sealed record PerformanceHeatmapCell(
    int SegmentIndex,
    double StartedAtSeconds,
    double EndedAtSeconds,
    string Metric,
    double Value,
    double Intensity,
    string DisplayValue);
