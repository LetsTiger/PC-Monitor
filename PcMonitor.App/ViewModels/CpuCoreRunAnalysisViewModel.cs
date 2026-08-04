using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class CpuCoreRunAnalysisViewModel
{
    public CpuCoreRunAnalysisViewModel(
        CpuCoreRunAnalysis analysis)
    {
        Analysis = analysis
                   ?? throw new ArgumentNullException(
                       nameof(analysis));
    }

    public CpuCoreRunAnalysis Analysis { get; }

    public string ThreadText =>
        $"Core {Analysis.CoreIndex}, " +
        $"Thread {Analysis.ThreadIndex}";

    public string SensorNameText =>
        Analysis.SensorName;

    public string AverageLoadText =>
        $"{Analysis.AverageLoadPercent:F1} %";

    public string MaximumLoadText =>
        $"{Analysis.MaximumLoadPercent:F1} %";

    public string Above80Text =>
        $"{Analysis.ShareAbove80Percent:F1} %";

    public string Above90Text =>
        $"{Analysis.ShareAbove90Percent:F1} %";

    public string Above95Text =>
        $"{Analysis.ShareAbove95Percent:F1} %";

    public string AssessmentText =>
        CreateAssessment(Analysis);

    private static string CreateAssessment(
        CpuCoreRunAnalysis analysis)
    {
        if (analysis.ShareAbove95Percent >= 20)
        {
            return
                "Sehr lange nahezu vollständig ausgelastet";
        }

        if (analysis.ShareAbove90Percent >= 10)
        {
            return
                "Häufig sehr hoch ausgelastet";
        }

        if (analysis.MaximumLoadPercent >= 95)
        {
            return
                "Kurze hohe Auslastungsspitze";
        }

        if (analysis.AverageLoadPercent >= 70)
        {
            return
                "Hohe durchschnittliche Auslastung";
        }

        return "Keine starke Auffälligkeit";
    }
}