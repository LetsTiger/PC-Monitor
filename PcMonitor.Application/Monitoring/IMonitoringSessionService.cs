using PcMonitor.Core.Metrics;
using PcMonitor.Core.Models;

namespace PcMonitor.Application.Monitoring;

public interface IMonitoringSessionService
{
    MonitoringRun? CurrentRun { get; }

    bool IsRunning { get; }

    event EventHandler<IMetricSample>? SampleCollected;

    string GenerateRunName();

    Task<MonitoringRun> StartAsync(
        string? runName,
        CancellationToken cancellationToken = default);

    Task<MonitoringRun> StopAsync();

    void RenameCurrentRun(string name);
}