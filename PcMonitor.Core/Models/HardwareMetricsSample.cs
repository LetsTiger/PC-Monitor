using PcMonitor.Core.Metrics;

namespace PcMonitor.Core.Models;

public sealed record HardwareMetricsSample(
    DateTimeOffset Timestamp,

    string? CpuName,
    double? CpuTemperatureCelsius,
    double? CpuMaximumCoreTemperatureCelsius,
    double? CpuAverageClockMegahertz,
    double? CpuPackagePowerWatts,

    string? GpuName,
    double? GpuLoadPercent,
    double? GpuTemperatureCelsius,
    double? GpuHotspotTemperatureCelsius,
    double? GpuCoreClockMegahertz,
    double? GpuMemoryClockMegahertz,
    double? GpuPowerWatts,
    double? GpuUsedMemoryMegabytes,
    double? GpuTotalMemoryMegabytes) : IMetricSample;