using PcMonitor.Core.Metrics;

namespace PcMonitor.Core.Models;

public sealed record DiskMetricsSample(
    DateTimeOffset Timestamp,

    string DeviceId,
    string DeviceName,

    double? TemperatureCelsius,

    double? ReadActivityPercent,
    double? WriteActivityPercent,
    double? TotalActivityPercent,

    double? ReadBytesPerSecond,
    double? WriteBytesPerSecond,

    double? UsedSpacePercent,
    double? FreeSpaceGigabytes,
    double? TotalSpaceGigabytes,

    double? RemainingLifePercent) : IMetricSample;