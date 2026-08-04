namespace PcMonitor.Core.Models;

public sealed record DiskRunAnalysis(
    Guid RunId,
    string DeviceId,
    string DeviceName,
    long SampleCount,

    double? AverageTemperatureCelsius,
    double? MaximumTemperatureCelsius,

    double? AverageReadActivityPercent,
    double? MaximumReadActivityPercent,

    double? AverageWriteActivityPercent,
    double? MaximumWriteActivityPercent,

    double? AverageTotalActivityPercent,
    double? MaximumTotalActivityPercent,

    double? AverageReadBytesPerSecond,
    double? MaximumReadBytesPerSecond,

    double? AverageWriteBytesPerSecond,
    double? MaximumWriteBytesPerSecond,

    double? MaximumUsedSpacePercent,
    double? MinimumFreeSpaceGigabytes,
    double? TotalSpaceGigabytes,

    double? MinimumRemainingLifePercent);