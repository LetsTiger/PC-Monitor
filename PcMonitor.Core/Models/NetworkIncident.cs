namespace PcMonitor.Core.Models;

public sealed record NetworkIncident(
    Guid Id,
    Guid RunId,
    NetworkIncidentTarget Target,
    string? Address,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    int FailureCount)
{
    public TimeSpan Duration => EndedAt - StartedAt;
}