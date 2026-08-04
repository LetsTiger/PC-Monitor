using PcMonitor.Core.Diagnostics;

namespace PcMonitor.Application.Diagnostics;

public interface IRunDiagnosticService
{
    IReadOnlyList<DiagnosticFinding> Analyze(
        RunDiagnosticContext context);
}