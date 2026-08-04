using PcMonitor.Core.Models;

namespace PcMonitor.Core.Services;

public sealed class RunService : IRunService
{
    public MonitoringRun? CurrentRun { get; private set; }

    public bool IsRunning =>
        CurrentRun?.Status == RunStatus.Running;

    public MonitoringRun StartRun(string? name)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException(
                "Es läuft bereits ein Monitoring-Run.");
        }

        string finalName = string.IsNullOrWhiteSpace(name)
            ? GenerateRunName()
            : name.Trim();

        CurrentRun = new MonitoringRun
        {
            Name = finalName,
            StartedAt = DateTimeOffset.Now,
            Status = RunStatus.Running
        };

        return CurrentRun;
    }

    public MonitoringRun StopRun()
    {
        if (CurrentRun is null || !IsRunning)
        {
            throw new InvalidOperationException(
                "Es läuft aktuell kein Monitoring-Run.");
        }

        CurrentRun.EndedAt = DateTimeOffset.Now;
        CurrentRun.Status = RunStatus.Completed;

        return CurrentRun;
    }

    public void RenameCurrentRun(string name)
    {
        if (CurrentRun is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        CurrentRun.Name = name.Trim();
    }

    public string GenerateRunName()
    {
        return $"Run {DateTime.Now:yyyy-MM-dd HH-mm-ss}";
    }
}