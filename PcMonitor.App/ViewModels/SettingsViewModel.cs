using System.ComponentModel;
using System.Runtime.CompilerServices;
using PcMonitor.Application.Settings;
using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IApplicationSettingsService _service;
    private PcMonitorSettings _currentSettings;

    public SettingsViewModel(IApplicationSettingsService service, PcMonitorSettings settings)
    {
        _service = service;
        _currentSettings = settings;
        Apply(settings);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int SystemIntervalMilliseconds { get; set; }
    public int ProcessIntervalMilliseconds { get; set; }
    public int MaximumProcessesPerSample { get; set; }
    public bool PresentMonEnabled { get; set; }
    public double TargetFramesPerSecond { get; set; }
    public double StutterThresholdMilliseconds { get; set; }
    public int MaximumHistoryRuns { get; set; }
    public string DatabasePath { get; set; } = "";
    public string StatusText { get; private set; } = "";

    public async Task SaveAsync()
    {
        PcMonitorSettings settings = _currentSettings with
        {
            SystemIntervalMilliseconds = Math.Clamp(SystemIntervalMilliseconds, 250, 10000),
            NetworkIntervalMilliseconds = Math.Clamp(SystemIntervalMilliseconds, 250, 10000),
            HardwareIntervalMilliseconds = Math.Clamp(SystemIntervalMilliseconds, 250, 10000),
            DiskIntervalMilliseconds = Math.Clamp(SystemIntervalMilliseconds, 250, 10000),
            ProcessIntervalMilliseconds = Math.Clamp(ProcessIntervalMilliseconds, 500, 10000),
            MaximumProcessesPerSample = Math.Clamp(MaximumProcessesPerSample, 1, 100),
            PresentMonEnabled = PresentMonEnabled,
            TargetFramesPerSecond = Math.Clamp(TargetFramesPerSecond, 15, 1000),
            StutterThresholdMilliseconds = Math.Clamp(StutterThresholdMilliseconds, 5, 500),
            MaximumHistoryRuns = Math.Clamp(MaximumHistoryRuns, 10, 10000),
            DatabasePath = DatabasePath
        };

        await _service.SaveAsync(settings);
        _currentSettings = settings;
        StatusText = "Einstellungen gespeichert. Collector- und Datenbankänderungen gelten nach Neustart.";
        OnPropertyChanged(nameof(StatusText));
    }

    private void Apply(PcMonitorSettings settings)
    {
        SystemIntervalMilliseconds = settings.SystemIntervalMilliseconds;
        ProcessIntervalMilliseconds = settings.ProcessIntervalMilliseconds;
        MaximumProcessesPerSample = settings.MaximumProcessesPerSample;
        PresentMonEnabled = settings.PresentMonEnabled;
        TargetFramesPerSecond = settings.TargetFramesPerSecond;
        StutterThresholdMilliseconds = settings.StutterThresholdMilliseconds;
        MaximumHistoryRuns = settings.MaximumHistoryRuns;
        DatabasePath = settings.DatabasePath;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
