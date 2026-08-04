using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class PerformanceTrendPointViewModel
{
    public PerformanceTrendPointViewModel(
        PerformanceTrendPoint point)
    {
        Point = point;
    }

    public PerformanceTrendPoint Point { get; }

    public string DateText =>
        Point.StartedAt.LocalDateTime
            .ToString("dd.MM.yyyy HH:mm");

    public string RunNameText => Point.RunName;
    public string ScoreText => $"{Point.Score:F1}";
    public string FpsText => Point.AverageFps is null ? "–" : $"{Point.AverageFps:F1}";
    public string LowText => Point.OnePercentLowFps is null ? "–" : $"{Point.OnePercentLowFps:F1}";
    public string P99Text => Point.P99FrameTimeMilliseconds is null ? "–" : $"{Point.P99FrameTimeMilliseconds:F1} ms";
    public string TemperatureText => Point.MaximumGpuTemperatureCelsius is null ? "–" : $"{Point.MaximumGpuTemperatureCelsius:F1} °C";
    public string StutterText => Point.StutterEpisodeCount.ToString("N0");
    public string BottleneckText => Point.Bottleneck.ToString();
}
