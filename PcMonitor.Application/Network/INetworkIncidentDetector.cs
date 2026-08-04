using PcMonitor.Core.Models;

namespace PcMonitor.Application.Network;

public interface INetworkIncidentDetector
{
    bool IsRunning { get; }

    Task StartAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    bool TryQueueSample(
        NetworkMetricsSample sample);

    Task StopAsync(
        DateTimeOffset endedAt,
        CancellationToken cancellationToken = default);
}