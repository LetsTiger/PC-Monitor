using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class PerformanceRegressionViewModel
{
    public PerformanceRegressionViewModel(
        PerformanceRegression regression)
    {
        Regression = regression;
    }

    public PerformanceRegression Regression { get; }

    public string StatusText =>
        Regression.Status;

    public string ExplanationText =>
        Regression.Explanation;

    public string ScoreText =>
        Regression.HasBaseline
            ? $"{Regression.BaselineScore:F1} → {Regression.CurrentScore:F1} " +
              $"({Regression.ScoreDifference:+0.0;-0.0;0.0})"
            : "Noch keine Basis";

    public string FpsText =>
        Regression.AverageFpsDifferencePercent is null
            ? "–"
            : $"{Regression.AverageFpsDifferencePercent:+0.0;-0.0;0.0} %";

    public string LowFpsText =>
        Regression.OnePercentLowDifferencePercent is null
            ? "–"
            : $"{Regression.OnePercentLowDifferencePercent:+0.0;-0.0;0.0} %";
}
