using System.Text.Json;
using PcMonitor.Application.Settings;
using PcMonitor.Core.Models;
namespace PcMonitor.Infrastructure.Settings;
public sealed class JsonApplicationSettingsService : IApplicationSettingsService
{
 private readonly string _path;
 private static readonly JsonSerializerOptions Options=new(){WriteIndented=true};
 public JsonApplicationSettingsService(string path){_path=Path.GetFullPath(path);}
 public async Task<PcMonitorSettings> LoadAsync(CancellationToken ct=default)
 {
  if(!File.Exists(_path)) return new PcMonitorSettings();
  try { await using var s=File.OpenRead(_path); return await JsonSerializer.DeserializeAsync<PcMonitorSettings>(s,Options,ct)??new(); }
  catch(JsonException){ return new(); }
 }
 public async Task SaveAsync(PcMonitorSettings settings,CancellationToken ct=default)
 {
  ArgumentNullException.ThrowIfNull(settings);
  Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
  string tmp=_path+".tmp";
  await using(var s=new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None))
   await JsonSerializer.SerializeAsync(s,settings,Options,ct);
  File.Move(tmp,_path,true);
 }
}
