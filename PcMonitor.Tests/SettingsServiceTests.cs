using PcMonitor.Core.Models;
using PcMonitor.Infrastructure.Settings;
namespace PcMonitor.Tests;
public sealed class SettingsServiceTests
{
 [Fact] public async Task RoundTrip_PreservesValues()
 {
  string dir=Path.Combine(Path.GetTempPath(),"PcMonitorTests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  string file=Path.Combine(dir,"settings.json");var sut=new JsonApplicationSettingsService(file);
  await sut.SaveAsync(new PcMonitorSettings{TargetFramesPerSecond=144,PresentMonEnabled=false,PresentMonStartupPreference=PresentMonStartupPreference.WithoutPresentMon});
  var result=await sut.LoadAsync();Assert.Equal(144,result.TargetFramesPerSecond);Assert.False(result.PresentMonEnabled);Assert.Equal(PresentMonStartupPreference.WithoutPresentMon,result.PresentMonStartupPreference);
  Directory.Delete(dir,true);
 }
}
