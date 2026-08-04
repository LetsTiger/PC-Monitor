using PcMonitor.Core.Models;

namespace PcMonitor.Application.History;

public interface IRunHistoryService
{
    Task<IReadOnlyList<RunSummary>> GetRunsAsync(
        CancellationToken cancellationToken = default);

    Task<RunSummary?> GetRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task RenameRunAsync(
        Guid runId,
        string newName,
        CancellationToken cancellationToken = default);

    Task DeleteRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}