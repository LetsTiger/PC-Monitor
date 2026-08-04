using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class GameSessionStatisticsViewModel
{
    public GameSessionStatisticsViewModel(
        GameSessionStatistics statistics)
    {
        Statistics = statistics;
    }

    public GameSessionStatistics Statistics { get; }

    public string TitleText =>
        Statistics.GameName;

    public string RunCountText =>
        Statistics.RunCount.ToString("N0");

    public string TotalDurationText =>
        $"{(int)Statistics.TotalDuration.TotalHours:00}:" +
        $"{Statistics.TotalDuration.Minutes:00}:" +
        $"{Statistics.TotalDuration.Seconds:00}";

    public string ScoreText =>
        $"{Statistics.AverageScore:F1} Ø / {Statistics.BestScore:F1} best";

    public string FpsText =>
        Statistics.AverageFps is null
            ? "–"
            : $"{Statistics.AverageFps:F1} FPS Ø / " +
              $"{Statistics.BestAverageFps:F1} best";

    public string LowFpsText =>
        Statistics.AverageOnePercentLowFps is null
            ? "–"
            : $"{Statistics.AverageOnePercentLowFps:F1} FPS";

    public string P99Text =>
        Statistics.AverageP99FrameTimeMilliseconds is null
            ? "–"
            : $"{Statistics.AverageP99FrameTimeMilliseconds:F1} ms";

    public string StutterText =>
        $"{Statistics.AverageStutterEpisodes:F1} Episoden/Run";

    public string BottleneckText =>
        Statistics.MostCommonBottleneck.ToString();
}
