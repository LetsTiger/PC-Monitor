namespace PcMonitor.Core.Models;

public sealed record RunSampleCounts(
    Guid RunId,
    long SystemSamples,
    long NetworkSamples,
    long HardwareSamples,
    long CpuCoreSamples,
    long DiskSamples,
    long ProcessSamples,
    long NetworkIncidents)
{
    public long TotalSamples =>
        SystemSamples +
        NetworkSamples +
        HardwareSamples +
        CpuCoreSamples +
        DiskSamples +
        ProcessSamples;
}