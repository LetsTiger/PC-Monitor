namespace PcMonitor.Core.Models;

public sealed record RunAnalysis(
    Guid RunId,

    long SystemSampleCount,
    double? AverageCpuUsagePercent,
    double? MaximumCpuUsagePercent,
    double? AverageMemoryUsagePercent,
    double? MaximumMemoryUsagePercent,
    ulong? MaximumUsedMemoryBytes,

    long NetworkSampleCount,

    double? AverageGatewayLatencyMilliseconds,
    long? MaximumGatewayLatencyMilliseconds,
    double? MaximumGatewayPacketLossPercent,
    double? AverageGatewayJitterMilliseconds,
    int GatewayFailureCount,
    int MaximumGatewayConsecutiveFailures,

    double? AverageInternetLatencyMilliseconds,
    long? MaximumInternetLatencyMilliseconds,
    double? MaximumInternetPacketLossPercent,
    double? AverageInternetJitterMilliseconds,
    int InternetFailureCount,
    int MaximumInternetConsecutiveFailures,

    long HardwareSampleCount,

    string? CpuName,
    double? AverageCpuTemperatureCelsius,
    double? MaximumCpuTemperatureCelsius,
    double? MaximumCpuCoreTemperatureCelsius,
    double? AverageCpuClockMegahertz,
    double? MinimumCpuClockMegahertz,
    double? MaximumCpuClockMegahertz,
    double? AverageCpuPackagePowerWatts,
    double? MaximumCpuPackagePowerWatts,

    string? GpuName,
    double? AverageGpuLoadPercent,
    double? MaximumGpuLoadPercent,
    double? AverageGpuTemperatureCelsius,
    double? MaximumGpuTemperatureCelsius,
    double? MaximumGpuHotspotTemperatureCelsius,
    double? AverageGpuCoreClockMegahertz,
    double? MinimumGpuCoreClockMegahertz,
    double? MaximumGpuCoreClockMegahertz,
    double? AverageGpuPowerWatts,
    double? MaximumGpuPowerWatts,
    double? MaximumGpuUsedMemoryMegabytes,
    double? GpuTotalMemoryMegabytes);