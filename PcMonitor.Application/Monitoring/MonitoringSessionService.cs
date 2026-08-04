using PcMonitor.Application.Collectors;
using PcMonitor.Application.Storage;
using PcMonitor.Core.Metrics;
using PcMonitor.Core.Models;
using PcMonitor.Core.Services;
using PcMonitor.Application.Network;

namespace PcMonitor.Application.Monitoring;

public sealed class MonitoringSessionService :
    IMonitoringSessionService,
    IAsyncDisposable
{
    private readonly IRunService _runService;
    private readonly ICollectorManager _collectorManager;

    private readonly IMonitoringStorageCoordinator
        _storageCoordinator;

    private readonly SemaphoreSlim
        _operationLock = new(1, 1);

    private bool _initialized;
    private bool _disposed;
    
    private readonly INetworkIncidentDetector
        _networkIncidentDetector;

    public MonitoringSessionService(
        IRunService runService,
        ICollectorManager collectorManager,
        IMonitoringStorageCoordinator storageCoordinator,
        INetworkIncidentDetector networkIncidentDetector)
    {
        _runService = runService
            ?? throw new ArgumentNullException(
                nameof(runService));

        _collectorManager = collectorManager
            ?? throw new ArgumentNullException(
                nameof(collectorManager));

        _storageCoordinator = storageCoordinator
            ?? throw new ArgumentNullException(
                nameof(storageCoordinator));

        _collectorManager.SampleCollected +=
            OnSampleCollected;
        
        _networkIncidentDetector =
            networkIncidentDetector
            ?? throw new ArgumentNullException(
                nameof(networkIncidentDetector));
    }

    public MonitoringRun? CurrentRun =>
        _runService.CurrentRun;

    public bool IsRunning =>
        _runService.IsRunning;

    public event EventHandler<IMetricSample>?
        SampleCollected;

    public string GenerateRunName()
    {
        return _runService.GenerateRunName();
    }

    public async Task<MonitoringRun> StartAsync(
        string? runName,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(
            cancellationToken);

        try
        {
            if (IsRunning)
            {
                throw new InvalidOperationException(
                    "Es läuft bereits ein Monitoring-Run.");
            }

            if (!_initialized)
            {
                await _storageCoordinator.InitializeAsync(
                    cancellationToken);

                _initialized = true;
            }

            MonitoringRun run =
                _runService.StartRun(runName);

            try
            {
                await _storageCoordinator.StartRunAsync(
                    run,
                    cancellationToken);

                await _networkIncidentDetector.StartAsync(
                    run.Id,
                    cancellationToken);

                await _collectorManager.StartAllAsync(
                    cancellationToken);

                return run;
            }
            catch
            {
                await TryStopCollectorsAsync();
                
                await TryStopNetworkIncidentDetectorAsync(
                    DateTimeOffset.Now);
                
                await TryAbortStorageAsync();

                if (_runService.IsRunning)
                {
                    _runService.StopRun();
                }

                throw;
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<MonitoringRun> StopAsync()
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync();

        try
        {
            if (!IsRunning)
            {
                throw new InvalidOperationException(
                    "Es läuft aktuell kein Monitoring-Run.");
            }

            await _collectorManager.StopAllAsync();

            MonitoringRun completedRun =
                _runService.StopRun();

            await _networkIncidentDetector.StopAsync(
                completedRun.EndedAt ?? DateTimeOffset.Now);

            await _storageCoordinator.CompleteRunAsync(
                completedRun);

            return completedRun;
        }
        catch
        {
            await TryStopCollectorsAsync();
            await TryStopNetworkIncidentDetectorAsync(DateTimeOffset.Now);
            await TryAbortStorageAsync();
            if (_runService.IsRunning)
            {
                _runService.StopRun();
            }
            throw;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public void RenameCurrentRun(string name)
    {
        ThrowIfDisposed();

        _runService.RenameCurrentRun(name);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _collectorManager.SampleCollected -=
            OnSampleCollected;

        if (IsRunning)
        {
            try
            {
                await StopAsync();
            }
            catch
            {
                await TryStopCollectorsAsync();
                await TryAbortStorageAsync();
            }
        }

        if (_collectorManager
            is IAsyncDisposable collectorDisposable)
        {
            await collectorDisposable.DisposeAsync();
        }
        
        if (_networkIncidentDetector
            is IAsyncDisposable incidentDetectorDisposable)
        {
            await incidentDetectorDisposable.DisposeAsync();
        }

        if (_storageCoordinator
            is IAsyncDisposable storageDisposable)
        {
            await storageDisposable.DisposeAsync();
        }

        _disposed = true;
        _operationLock.Dispose();
    }

    private void OnSampleCollected(
        object? sender,
        IMetricSample sample)
    {
        if (!IsRunning)
        {
            return;
        }

        SampleCollected?.Invoke(sender, sample);

        _storageCoordinator.TryQueueSample(sample);

        if (sample is NetworkMetricsSample networkSample)
        {
            _networkIncidentDetector.TryQueueSample(
                networkSample);
        }
    }

    private async Task TryStopCollectorsAsync()
    {
        try
        {
            await _collectorManager.StopAllAsync();
        }
        catch
        {
            // Ursprünglichen Fehler nicht überschreiben.
        }
    }

    private async Task TryAbortStorageAsync()
    {
        try
        {
            await _storageCoordinator.AbortRunAsync();
        }
        catch
        {
            // Ursprünglichen Fehler nicht überschreiben.
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
    
    private async Task TryStopNetworkIncidentDetectorAsync(
        DateTimeOffset endedAt)
    {
        try
        {
            await _networkIncidentDetector.StopAsync(
                endedAt);
        }
        catch
        {
            // Ursprünglichen Fehler nicht überschreiben.
        }
    }
}