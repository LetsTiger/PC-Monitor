using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class FrameRunAnalysisViewModel
{
    public FrameRunAnalysisViewModel(
        FrameRunAnalysis analysis)
    {
        Analysis =
            analysis
            ?? throw new ArgumentNullException(
                nameof(analysis));
    }

    public FrameRunAnalysis Analysis { get; }

    public string ApplicationText =>
        $"{Analysis.Application} ({Analysis.ProcessId})";

    public string FrameCountText =>
        Analysis.FrameCount.ToString("N0");

    public string AverageFpsText =>
        $"{Analysis.AverageFramesPerSecond:F1} FPS";

    public string OnePercentLowText =>
        $"{Analysis.OnePercentLowFramesPerSecond:F1} FPS";

    public string PointOnePercentLowText =>
        $"{Analysis.PointOnePercentLowFramesPerSecond:F1} FPS";

    public string AverageFrameTimeText =>
        $"{Analysis.AverageFrameTimeMilliseconds:F2} ms";

    public string P95FrameTimeText =>
        $"{Analysis.P95FrameTimeMilliseconds:F2} ms";

    public string P99FrameTimeText =>
        $"{Analysis.P99FrameTimeMilliseconds:F2} ms";

    public string MaximumFrameTimeText =>
        $"{Analysis.MaximumFrameTimeMilliseconds:F2} ms";

    public string SpikeCountText =>
        $">25 ms: {Analysis.FrameSpikesAbove25Milliseconds:N0}, " +
        $">50 ms: {Analysis.FrameSpikesAbove50Milliseconds:N0}, " +
        $">100 ms: {Analysis.FrameSpikesAbove100Milliseconds:N0}";

    public string SelectionText =>
        Analysis.IsLikelyGame
            ? "Wahrscheinliches Spiel"
            : "Weiterer Grafikprozess";

    public string SelectionScoreText =>
        Analysis.SelectionScore.ToString("F1");

    public string MatchedApplicationText
    {
        get
        {
            if (!Analysis.IsLikelyGame)
            {
                return "–";
            }

            if (string.IsNullOrWhiteSpace(
                    Analysis.MatchedToProcessName))
            {
                return "Keine Hauptanwendung zugeordnet";
            }

            return Analysis.MatchedToProcessId is null
                ? Analysis.MatchedToProcessName
                : $"{Analysis.MatchedToProcessName} " +
                  $"({Analysis.MatchedToProcessId})";
        }
    }

    public string MatchReasonText =>
        Analysis.MatchReason switch
        {
            FrameProcessMatchReason.ExactProcessId =>
                "Exakte Prozess-ID",

            FrameProcessMatchReason.ExactProcessName =>
                "Gleicher Prozessname",

            FrameProcessMatchReason.SharedInstallationDirectory =>
                "Gleicher Installationsordner",

            FrameProcessMatchReason.SharedPublisherDirectory =>
                "Gemeinsamer Hersteller-/Launcher-Ordner",

            FrameProcessMatchReason.FallbackScore =>
                "Fallback-Bewertung",

            _ => "–"
        };

    public string ProcessEvidenceText =>
        $"{Analysis.ForegroundSharePercent:F1} % Vordergrund, " +
        $"{Analysis.AverageProcessCpuPercent:F1} % CPU Ø, " +
        $"{Analysis.CaptureDurationSeconds:F0} s Capture";
}
