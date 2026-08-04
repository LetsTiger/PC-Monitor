using PcMonitor.Core.Models;

namespace PcMonitor.Core.Services;

public interface IRunService
{
    MonitoringRun? CurrentRun { get; }

    bool IsRunning { get; }

    MonitoringRun StartRun(string? name);

    MonitoringRun StopRun();

    void RenameCurrentRun(string name);

    string GenerateRunName();
}