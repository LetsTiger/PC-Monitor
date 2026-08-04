using PcMonitor.Core.Models;
using System.Windows.Media;

namespace PcMonitor.App.ViewModels;

public sealed class PerformanceScoreViewModel
{
    public PerformanceScoreViewModel(
        PerformanceScoreResult score)
    {
        Score = score;
    }

    public PerformanceScoreResult Score { get; }

    public string TotalScoreText =>
        $"{Score.TotalScore:F0} / 100";

    public string GradeText =>
        Score.Grade;

    public Brush RatingBrush =>
        Score.TotalScore >= 75
            ? new SolidColorBrush(Color.FromRgb(52, 168, 83))
            : Score.TotalScore >= 55
                ? new SolidColorBrush(Color.FromRgb(224, 157, 45))
                : new SolidColorBrush(Color.FromRgb(214, 69, 69));

    public string SummaryText =>
        Score.Summary;

    public string BreakdownText =>
        $"FPS {Score.FpsScore:F0} · 1-%-Low {Score.LowFpsScore:F0} · " +
        $"Frametime {Score.FrameTimeScore:F0} · Stabilität {Score.StabilityScore:F0} · " +
        $"Hardware {Score.HardwareScore:F0} · Netzwerk {Score.NetworkScore:F0}";

    public string BreakdownExplanationText =>
        "Alle Teilwerte sind Bewertungen von 0 bis 100 – höher ist besser. " +
        "FPS bewertet die durchschnittliche Bildrate. 1-%-Low bewertet die langsamsten " +
        "1 % der Bilder und zeigt kurze Einbrüche. Frametime bewertet, wie schnell einzelne " +
        "Bilder erzeugt werden. Stabilität bewertet Schwankungen und Ruckler. Hardware " +
        "berücksichtigt unter anderem Auslastung und Temperaturen, Netzwerk Paketverlust, " +
        "Jitter und Verbindungsabbrüche.";
}
