using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class ProcessRunAnalysisViewModel
{
    public ProcessRunAnalysisViewModel(
        ProcessRunAnalysis analysis)
    {
        Analysis = analysis
            ?? throw new ArgumentNullException(
                nameof(analysis));
    }

    public ProcessRunAnalysis Analysis { get; }

    public string ProcessNameText =>
        Analysis.ProcessName;

    public string CpuText =>
        $"{Analysis.AverageCpuUsagePercent:F1} % Ø / " +
        $"{Analysis.MaximumCpuUsagePercent:F1} % max.";

    public string WorkingSetText =>
        FormatBytes(
            Analysis.MaximumWorkingSetBytes);

    public string PrivateMemoryText =>
        FormatBytes(
            Analysis.MaximumPrivateMemoryBytes);

    public string ReadRateText =>
        FormatRate(
            Analysis.MaximumReadBytesPerSecond);

    public string WriteRateText =>
        FormatRate(
            Analysis.MaximumWriteBytesPerSecond);

    public string ForegroundText =>
        Analysis.ForegroundSampleCount <= 0
            ? "Nein"
            : $"{Analysis.ForegroundSharePercent:F1} %";

    public string ThreadsText =>
        Analysis.MaximumThreadCount.ToString("N0");

    public string HandlesText =>
        Analysis.MaximumHandleCount.ToString("N0");

    public string AssessmentText =>
        CreateAssessment(Analysis);

    private static string CreateAssessment(
        ProcessRunAnalysis analysis)
    {
        if (analysis.ForegroundSharePercent >= 50)
        {
            return "Wahrscheinliche Hauptanwendung";
        }

        if (analysis.MaximumCpuUsagePercent >= 50)
        {
            return "Hohe CPU-Spitze";
        }

        if (analysis.MaximumWorkingSetBytes >=
            4L * 1024 * 1024 * 1024)
        {
            return "Sehr hoher RAM-Verbrauch";
        }

        if (analysis.MaximumWriteBytesPerSecond >=
            100d * 1024 * 1024)
        {
            return "Hohe Schreibaktivität";
        }

        if (analysis.MaximumReadBytesPerSecond >=
            250d * 1024 * 1024)
        {
            return "Hohe Leseaktivität";
        }

        return "Keine starke Auffälligkeit";
    }

    private static string FormatBytes(
        long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024)
        {
            return
                $"{bytes / (1024d * 1024d * 1024d):F2} GB";
        }

        if (bytes >= 1024L * 1024)
        {
            return
                $"{bytes / (1024d * 1024d):F1} MB";
        }

        return
            $"{bytes / 1024d:F1} KB";
    }

    private static string FormatRate(
        double? bytesPerSecond)
    {
        if (bytesPerSecond is null)
        {
            return "–";
        }

        double value =
            bytesPerSecond.Value;

        if (value >= 1024d * 1024d * 1024d)
        {
            return
                $"{value / (1024d * 1024d * 1024d):F2} GB/s";
        }

        if (value >= 1024d * 1024d)
        {
            return
                $"{value / (1024d * 1024d):F1} MB/s";
        }

        if (value >= 1024d)
        {
            return
                $"{value / 1024d:F1} KB/s";
        }

        return $"{value:F0} B/s";
    }
}