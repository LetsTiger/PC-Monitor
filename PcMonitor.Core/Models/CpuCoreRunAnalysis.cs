namespace PcMonitor.Core.Models;

public sealed record CpuCoreRunAnalysis(
    Guid RunId,

    int CoreIndex,
    int ThreadIndex,
    string SensorName,

    long SampleCount,

    double AverageLoadPercent,
    double MaximumLoadPercent,

    long SamplesAbove80Percent,
    long SamplesAbove90Percent,
    long SamplesAbove95Percent)
{
    public double ShareAbove80Percent =>
        CalculateShare(SamplesAbove80Percent);

    public double ShareAbove90Percent =>
        CalculateShare(SamplesAbove90Percent);

    public double ShareAbove95Percent =>
        CalculateShare(SamplesAbove95Percent);

    private double CalculateShare(
        long matchingSamples)
    {
        return SampleCount <= 0
            ? 0
            : matchingSamples /
              (double)SampleCount *
              100.0;
    }
}