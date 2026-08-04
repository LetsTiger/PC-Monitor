using PcMonitor.Core.Models;

namespace PcMonitor.Application.Settings;

public interface IApplicationSettingsService
{
    Task<PcMonitorSettings> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        PcMonitorSettings settings,
        CancellationToken cancellationToken = default);
}