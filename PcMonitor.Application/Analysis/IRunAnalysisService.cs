using PcMonitor.Core.Models;
using PcMonitor.Core.Timeline;

namespace PcMonitor.Application.Analysis;

public interface IRunAnalysisService
{
    Task<RunAnalysis?> GetRunAnalysisAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<NetworkIncident>>
        GetNetworkIncidentsAsync(
            Guid runId,
            CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DiskRunAnalysis>>
        GetDiskRunAnalysesAsync(
            Guid runId,
            CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<ProcessRunAnalysis>>
        GetProcessRunAnalysesAsync(
            Guid runId,
            CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<CpuCoreRunAnalysis>>
        GetCpuCoreRunAnalysesAsync(
            Guid runId,
            CancellationToken cancellationToken = default);
    
    Task<RunSampleCounts?> GetRunSampleCountsAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<RunTimelineEvent>>
        GetRunTimelineEventsAsync(
            Guid runId,
            int maximumEventCount = 500,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FrameRunAnalysis>>
        GetFrameRunAnalysesAsync(
            Guid runId,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FrameChartPoint>>
        GetFrameChartPointsAsync(
            Guid runId,
            int maximumPointCount = 1500,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RunTimelineEvent>>
        GetPerformanceTimelineEventsAsync(
            Guid runId,
            int maximumEventCount = 500,
            CancellationToken cancellationToken = default);
    Task<RunApplicationOverride?> GetRunApplicationOverrideAsync(Guid runId, CancellationToken cancellationToken = default);
    Task SaveRunApplicationOverrideAsync(RunApplicationOverride value, CancellationToken cancellationToken = default);
    Task<PersistedAnalysisResult?> GetPersistedAnalysisResultAsync(Guid runId, CancellationToken cancellationToken = default);
    Task SavePersistedAnalysisResultAsync(PersistedAnalysisResult value, CancellationToken cancellationToken = default);
}
