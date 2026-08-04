namespace PcMonitor.Core.Models;

public sealed record RunSummary(
    Guid Id,
    string Name,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    RunStatus Status,
    long SampleCount)
{
    public TimeSpan Duration =>
        (EndedAt ?? DateTimeOffset.Now) - StartedAt;
}