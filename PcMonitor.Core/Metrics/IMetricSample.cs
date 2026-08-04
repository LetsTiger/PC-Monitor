namespace PcMonitor.Core.Metrics;

public interface IMetricSample
{
    DateTimeOffset Timestamp { get; }
}