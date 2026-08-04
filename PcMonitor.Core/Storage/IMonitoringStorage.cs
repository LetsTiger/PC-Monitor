using PcMonitor.Core.Metrics;
using PcMonitor.Core.Models;
using PcMonitor.Core.Timeline;

namespace PcMonitor.Core.Storage;

public interface IMonitoringStorage
{
    Task InitializeAsync(
        CancellationToken cancellationToken = default);

    Task CreateRunAsync(
        MonitoringRun run,
        CancellationToken cancellationToken = default);

    Task CompleteRunAsync(
        MonitoringRun run,
        CancellationToken cancellationToken = default);

    Task SaveSampleAsync(
        Guid runId,
        IMetricSample sample,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RunSummary>> GetRunsAsync(
        CancellationToken cancellationToken = default);

    Task<RunSummary?> GetRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SystemMetricsSample>>
        GetSystemMetricSamplesAsync(
            Guid runId,
            CancellationToken cancellationToken = default);
    
    Task RenameRunAsync(
        Guid runId,
        string newName,
        CancellationToken cancellationToken = default);

    Task DeleteRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
    
    Task<RunAnalysis?> GetRunAnalysisAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
    
    Task SaveNetworkIncidentAsync(
        NetworkIncident incident,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NetworkIncident>> GetNetworkIncidentsAsync(
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
    
    Task SaveSamplesAsync(
        Guid runId,
        IReadOnlyList<IMetricSample> samples,
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
    Task<int> RecoverInterruptedRunsAsync(DateTimeOffset recoveredAt, CancellationToken cancellationToken = default);
}
