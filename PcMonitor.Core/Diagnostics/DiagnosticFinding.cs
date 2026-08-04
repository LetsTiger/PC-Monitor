namespace PcMonitor.Core.Diagnostics;

public sealed record DiagnosticFinding(
    string Code,
    DiagnosticSeverity Severity,
    DiagnosticCategory Category,
    string Title,
    string Description,
    string? Recommendation = null);