namespace PcMonitor.Core.Models;

public sealed record BottleneckAnalysis(
    Guid RunId,
    BottleneckType PrimaryType,
    double ConfidencePercent,
    string Title,
    string Description,
    string Recommendation,
    double GpuScore,
    double CpuScore,
    double MemoryScore,
    double VramScore,
    double DiskScore,
    double NetworkScore,
    double BackgroundProcessScore);
