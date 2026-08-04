using PcMonitor.Core.Models;

namespace PcMonitor.Application.Diagnostics;

public sealed record RunDiagnosticContext(
    RunAnalysis? RunAnalysis,
    IReadOnlyList<NetworkIncident> NetworkIncidents,
    IReadOnlyList<DiskRunAnalysis> DiskAnalyses,
    IReadOnlyList<ProcessRunAnalysis> ProcessAnalyses,
    IReadOnlyList<CpuCoreRunAnalysis> CpuCoreAnalyses,
    IReadOnlyList<FrameRunAnalysis> FrameAnalyses,
    BottleneckAnalysis? BottleneckAnalysis);
