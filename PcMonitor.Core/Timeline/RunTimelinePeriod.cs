using PcMonitor.Core.Diagnostics;

namespace PcMonitor.Core.Timeline;

public sealed record RunTimelinePeriod(
    Guid RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    DiagnosticSeverity Severity,
    TimelineEventCategory Category,
    string EventCode,
    string SourceId,
    string SourceName,
    string Title,
    string Description,
    int EventCount,
    double? AverageNumericValue,
    double? MaximumNumericValue,
    string? Unit,
    bool IsCorrelation)
{
    public TimeSpan Duration =>
        EndedAt >= StartedAt
            ? EndedAt - StartedAt
            : TimeSpan.Zero;
}