namespace PcMonitor.App.Services;

public interface IDiagnosticsPackageService
{
    Task<string> CreateAsync(CancellationToken cancellationToken = default);
}
