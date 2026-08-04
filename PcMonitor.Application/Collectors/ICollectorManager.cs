using PcMonitor.Core.Metrics;

namespace PcMonitor.Application.Collectors;

public interface ICollectorManager
{
    bool IsRunning { get; }

    IReadOnlyCollection<string> CollectorNames { get; }

    event EventHandler<IMetricSample>? SampleCollected;

    Task StartAllAsync(
        CancellationToken cancellationToken = default);

    Task StopAllAsync();
}