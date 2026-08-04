using PcMonitor.Core.Metrics;

namespace PcMonitor.Core.Collectors;

public interface IMetricCollector
{
    string Name { get; }

    bool IsRunning { get; }

    event EventHandler<IMetricSample>? SampleCollected;

    Task StartAsync(
        CancellationToken cancellationToken = default);

    Task StopAsync();
}