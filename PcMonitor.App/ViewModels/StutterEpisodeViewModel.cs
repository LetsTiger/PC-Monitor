using PcMonitor.Core.Models;

using System.Windows.Media;

namespace PcMonitor.App.ViewModels;

public sealed class StutterEpisodeViewModel
{
    public StutterEpisodeViewModel(
        StutterEpisode episode)
    {
        Episode =
            episode
            ?? throw new ArgumentNullException(
                nameof(episode));
    }

    public StutterEpisode Episode { get; }

    public string TimeText =>
        Episode.StartedAt.LocalDateTime
            .ToString("HH:mm:ss.fff");

    public string DurationText =>
        Episode.Duration.TotalSeconds < 1
            ? $"{Episode.Duration.TotalMilliseconds:F0} ms"
            : $"{Episode.Duration.TotalSeconds:F2} s";

    public string SeverityText =>
        Episode.MaximumFrameTimeMilliseconds >= 100
            ? "Kritisch"
            : Episode.MaximumFrameTimeMilliseconds >= 50
                ? "Warnung"
                : "Hinweis";

    public Brush SeverityBrush =>
        Episode.MaximumFrameTimeMilliseconds >= 100
            ? new SolidColorBrush(Color.FromRgb(214, 69, 69))
            : Episode.MaximumFrameTimeMilliseconds >= 50
                ? new SolidColorBrush(Color.FromRgb(224, 157, 45))
                : new SolidColorBrush(Color.FromRgb(78, 155, 214));

    public string TitleText =>
        Episode.Title;

    public string CauseText =>
        Episode.Cause.ToString();

    public string ConfidenceText =>
        $"{Episode.ConfidencePercent:F0} %";

    public string FrameTimeText =>
        $"{Episode.AverageFrameTimeMilliseconds:F1} ms Ø / " +
        $"{Episode.MaximumFrameTimeMilliseconds:F1} ms max.";

    public string FpsText =>
        $"{Episode.MinimumFramesPerSecond:F1} FPS min.";

    public string SpikeText =>
        $"{Episode.SpikeCount:N0} Spike(s)";

    public string ContextText
    {
        get
        {
            var parts =
                new List<string>();

            if (Episode.MaximumCpuThreadLoadPercent is not null)
            {
                parts.Add(
                    $"CPU-Thread {Episode.MaximumCpuThreadLoadPercent:F1} %");
            }

            if (Episode.AverageGpuLoadPercent is not null)
            {
                parts.Add(
                    $"GPU {Episode.AverageGpuLoadPercent:F1} %");
            }

            if (Episode.MaximumMemoryUsagePercent is not null)
            {
                parts.Add(
                    $"RAM {Episode.MaximumMemoryUsagePercent:F1} %");
            }

            if (Episode.MaximumVramUsagePercent is not null)
            {
                parts.Add(
                    $"VRAM {Episode.MaximumVramUsagePercent:F1} %");
            }

            if (Episode.MaximumDiskActivityPercent is not null)
            {
                parts.Add(
                    $"Disk {Episode.MaximumDiskActivityPercent:F1} %");
            }

            if (!string.IsNullOrWhiteSpace(
                    Episode.BackgroundProcessName))
            {
                parts.Add(
                    $"{Episode.BackgroundProcessName} " +
                    $"{Episode.BackgroundProcessCpuPercent:F1} % CPU");
            }

            return parts.Count == 0
                ? "Keine Kontextwerte"
                : string.Join(", ", parts);
        }
    }

    public string DescriptionText =>
        Episode.Description;
}
