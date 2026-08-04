using PcMonitor.Infrastructure.Settings;
using PcMonitor.Core.Models;
namespace PcMonitor.Tests;
public sealed class SettingsCorruptionTests
{
    [Fact]
    public async Task InvalidJson_ReturnsDefaults()
    {
        string directory = Path.Combine(Path.GetTempPath(), "PcMonitorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "settings.json");
        await File.WriteAllTextAsync(path, "{ invalid json");
        var result = await new JsonApplicationSettingsService(path).LoadAsync();
        Assert.True(result.PresentMonEnabled);
        Assert.Equal(PresentMonStartupPreference.Ask, result.PresentMonStartupPreference);
        Directory.Delete(directory, true);
    }
}
