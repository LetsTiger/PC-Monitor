namespace PcMonitor.Core.Models;

public sealed record ProcessRunAnalysis(
    Guid RunId,
    string ProcessName,
    string? ExecutablePath,

    long SampleCount,
    long ForegroundSampleCount,

    double AverageCpuUsagePercent,
    double MaximumCpuUsagePercent,

    long MaximumWorkingSetBytes,
    long MaximumPrivateMemoryBytes,

    int MaximumThreadCount,
    int MaximumHandleCount,

    double? AverageReadBytesPerSecond,
    double? MaximumReadBytesPerSecond,

    double? AverageWriteBytesPerSecond,
    double? MaximumWriteBytesPerSecond)
{
    public double ForegroundSharePercent =>
        SampleCount <= 0
            ? 0
            : ForegroundSampleCount /
              (double)SampleCount *
              100.0;
}