namespace PcMonitor.Core.Models;
public sealed record PcMonitorSettings
{
 public int SystemIntervalMilliseconds { get; init; } = 1000;
 public int NetworkIntervalMilliseconds { get; init; } = 1000;
 public int HardwareIntervalMilliseconds { get; init; } = 1000;
 public int DiskIntervalMilliseconds { get; init; } = 1000;
 public int ProcessIntervalMilliseconds { get; init; } = 2000;
 public int MaximumProcessesPerSample { get; init; } = 8;
 public bool PresentMonEnabled { get; init; } = true;
 public PresentMonStartupPreference PresentMonStartupPreference { get; init; } = PresentMonStartupPreference.Ask;
 public int PresentMonCircularBufferSize { get; init; } = 65536;
 public double TargetFramesPerSecond { get; init; } = 60;
 public double StutterThresholdMilliseconds { get; init; } = 25;
 public int MaximumHistoryRuns { get; init; } = 100;
 public string DatabasePath { get; init; } = string.Empty;
 public IReadOnlyList<string> ExcludedFrameApplications { get; init; } = [];
}
