using PcMonitor.Core.Models;
using PcMonitor.Core.Storage;

namespace PcMonitor.Application.History;

public sealed class RunHistoryService : IRunHistoryService
{
    private readonly IMonitoringStorage _storage;
    private readonly SemaphoreSlim _initializationLock =
        new(1, 1);

    private bool _initialized;

    public RunHistoryService(
        IMonitoringStorage storage)
    {
        _storage = storage
                   ?? throw new ArgumentNullException(
                       nameof(storage));
    }

    public async Task<IReadOnlyList<RunSummary>> GetRunsAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(
            cancellationToken);

        return await _storage.GetRunsAsync(
            cancellationToken);
    }

    public async Task<RunSummary?> GetRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(
            cancellationToken);

        return await _storage.GetRunAsync(
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
    
    public async Task RenameRunAsync(
        Guid runId,
        string newName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ArgumentException(
                "Der Run-Name darf nicht leer sein.",
                nameof(newName));
        }

        await EnsureInitializedAsync(
            cancellationToken);

        await _storage.RenameRunAsync(
            runId,
            newName.Trim(),
            cancellationToken);
    }

    public async Task DeleteRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(
            cancellationToken);

        await _storage.DeleteRunAsync(
            runId,
            cancellationToken);
    }
}