using PcMonitor.Core.Models;
using PcMonitor.Core.Storage;
using PcMonitor.Core.Timeline;

namespace PcMonitor.Application.Analysis;

public sealed class RunAnalysisService :
    IRunAnalysisService
{
    private readonly IMonitoringStorage _storage;
    private readonly SemaphoreSlim _initializationLock =
        new(1, 1);

    private bool _initialized;

    public RunAnalysisService(
        IMonitoringStorage storage)
    {
        _storage = storage
                   ?? throw new ArgumentNullException(
                       nameof(storage));
    }

    public async Task<RunAnalysis?> GetRunAnalysisAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(
            cancellationToken);

        return await _storage.GetRunAnalysisAsync(
            runId,
            cancellationToken);
    }

    private async Task EnsureInitializedAsync(
        CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(
            cancellationToken);

        try
        {
            if (_initialized)
            {
                return;
            }

            await _storage.InitializeAsync(
                cancellationToken);

            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }
    
    public async Task<IReadOnlyList<NetworkIncident>>
        GetNetworkIncidentsAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(
            cancellationToken);

        return await _storage.GetNetworkIncidentsAsync(
            runId,
            cancellationToken);
    }
    
    public async Task<IReadOnlyList<DiskRunAnalysis>>
        GetDiskRunAnalysesAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(
            cancellationToken);

        return await _storage.GetDiskRunAnalysesAsync(
            runId,
            cancellationToken);
    }
    
    public async Task<IReadOnlyList<ProcessRunAnalysis>>
        GetProcessRunAnalysesAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(
            cancellationToken);

        return await _storage.GetProcessRunAnalysesAsync(
            runId,
            cancellationToken);
    }
    
    public async Task<IReadOnlyList<CpuCoreRunAnalysis>>
        GetCpuCoreRunAnalysesAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(
            cancellationToken);

        return await _storage.GetCpuCoreRunAnalysesAsync(
            runId,
            cancellationToken);
    }
    
    public async Task<RunSampleCounts?>
        GetRunSampleCountsAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(
            cancellationToken);

        return await _storage.GetRunSampleCountsAsync(
            runId,
            cancellationToken);
    }
    
    public async Task<IReadOnlyList<RunTimelineEvent>>
        GetRunTimelineEventsAsync(
            Guid runId,
            int maximumEventCount = 500,
            CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(
            cancellationToken);

        return await _storage.GetRunTimelineEventsAsync(
            runId,
            maximumEventCount,
            cancellationToken);
    }

    public async Task<IReadOnlyList<FrameRunAnalysis>>
        GetFrameRunAnalysesAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        return await _storage.GetFrameRunAnalysesAsync(
            runId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<FrameChartPoint>>
        GetFrameChartPointsAsync(
            Guid runId,
            int maximumPointCount = 1500,
            CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        return await _storage.GetFrameChartPointsAsync(
            runId,
            maximumPointCount,
            cancellationToken);
    }

    public async Task<IReadOnlyList<RunTimelineEvent>>
        GetPerformanceTimelineEventsAsync(
            Guid runId,
            int maximumEventCount = 500,
            CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        return await _storage.GetPerformanceTimelineEventsAsync(
            runId,
            maximumEventCount,
            cancellationToken);
    }


    public async Task<RunApplicationOverride?> GetRunApplicationOverrideAsync(Guid runId,CancellationToken cancellationToken=default){await EnsureInitializedAsync(cancellationToken);return await _storage.GetRunApplicationOverrideAsync(runId,cancellationToken);}
    public async Task SaveRunApplicationOverrideAsync(RunApplicationOverride value,CancellationToken cancellationToken=default){await EnsureInitializedAsync(cancellationToken);await _storage.SaveRunApplicationOverrideAsync(value,cancellationToken);}
    public async Task<PersistedAnalysisResult?> GetPersistedAnalysisResultAsync(Guid runId,CancellationToken cancellationToken=default){await EnsureInitializedAsync(cancellationToken);return await _storage.GetPersistedAnalysisResultAsync(runId,cancellationToken);}
    public async Task SavePersistedAnalysisResultAsync(PersistedAnalysisResult value,CancellationToken cancellationToken=default){await EnsureInitializedAsync(cancellationToken);await _storage.SavePersistedAnalysisResultAsync(value,cancellationToken);}
}
