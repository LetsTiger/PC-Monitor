namespace PcMonitor.Core.Models;

public sealed record TimelineStory(
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    string Title,
    string Cause,
    string Impact,
    string Recovery,
    double ConfidencePercent)
{
    public TimeSpan Duration =>
        EndedAt >= StartedAt
            ? EndedAt - StartedAt
            : TimeSpan.Zero;
}
