namespace PcMonitor.Application.Export;

public interface IRunExportService
{
    Task ExportCsvAsync(
        Guid runId,
        string filePath,
        CancellationToken cancellationToken = default);

    Task ExportJsonAsync(
        Guid runId,
        string filePath,
        CancellationToken cancellationToken = default);
}