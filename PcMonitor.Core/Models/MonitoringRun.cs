namespace PcMonitor.Core.Models;

public sealed class MonitoringRun
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? EndedAt { get; set; }

    public RunStatus Status { get; set; } = RunStatus.Ready;

    public TimeSpan Duration
    {
        get
        {
            DateTimeOffset endTime = EndedAt ?? DateTimeOffset.Now;
            return endTime - StartedAt;
        }
    }
}