using PcMonitor.Core.Metrics;
using PcMonitor.Core.Models;

namespace PcMonitor.Application.Storage;

public interface IMonitoringStorageCoordinator
{
    bool IsRunning { get; }

    Task InitializeAsync(
        CancellationToken cancellationToken = default);

    Task StartRunAsync(
        MonitoringRun run,
        CancellationToken cancellationToken = default);

    bool TryQueueSample(IMetricSample sample);

    Task CompleteRunAsync(
        MonitoringRun run,
        CancellationToken cancellationToken = default);

    Task AbortRunAsync(
        CancellationToken cancellationToken = default);
}