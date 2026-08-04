using PcMonitor.Core.Diagnostics;

namespace PcMonitor.Core.Timeline;

public sealed record RunTimelineEvent(
    Guid RunId,
    DateTimeOffset Timestamp,
    DiagnosticSeverity Severity,
    TimelineEventCategory Category,
    string EventCode,
    string SourceId,
    string SourceName,
    string Title,
    string Description,
    double? NumericValue = null,
    string? Unit = null,
    bool IsCorrelation = false);