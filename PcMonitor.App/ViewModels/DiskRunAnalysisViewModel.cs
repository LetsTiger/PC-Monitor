using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class DiskRunAnalysisViewModel
{
    public DiskRunAnalysisViewModel(
        DiskRunAnalysis analysis)
    {
        Analysis = analysis
            ?? throw new ArgumentNullException(
                nameof(analysis));
    }

    public DiskRunAnalysis Analysis { get; }

    public string DeviceNameText =>
        Analysis.DeviceName;

    public string TemperatureText =>
        JoinValues(
            FormatTemperature(
                Analysis.AverageTemperatureCelsius),
            FormatTemperature(
                Analysis.MaximumTemperatureCelsius));

    public string ActivityText =>
        JoinValues(
            FormatPercent(
                Analysis.AverageTotalActivityPercent),
            FormatPercent(
                Analysis.MaximumTotalActivityPercent));

    public string ReadRateText =>
        JoinValues(
            FormatRate(
                Analysis.AverageReadBytesPerSecond),
            FormatRate(
                Analysis.MaximumReadBytesPerSecond));

    public string WriteRateText =>
        JoinValues(
            FormatRate(
                Analysis.AverageWriteBytesPerSecond),
            FormatRate(
                Analysis.MaximumWriteBytesPerSecond));

    public string SpaceText
    {
        get
        {
            string used =
                FormatPercent(
                    Analysis.MaximumUsedSpacePercent);

            string free =
                Analysis.MinimumFreeSpaceGigabytes is null
                    ? "–"
                    : $"{Analysis.MinimumFreeSpaceGigabytes.Value:F1} GB frei";

            return $"{used} belegt, {free}";
        }
    }

    public string LifeText =>
        Analysis.MinimumRemainingLifePercent is null
            ? "Nicht verfügbar"
            : $"{Analysis.MinimumRemainingLifePercent.Value:F0} %";

    public string AssessmentText =>
        CreateAssessment(Analysis);

    private static string CreateAssessment(
        DiskRunAnalysis analysis)
    {
        if (analysis.MaximumTemperatureCelsius >= 80)
        {
            return "Sehr hohe Temperatur";
        }

        if (analysis.MaximumTemperatureCelsius >= 70)
        {
            return "Erhöhte Temperatur";
        }

        if (analysis.MaximumTotalActivityPercent >= 95)
        {
            return "Zeitweise vollständig ausgelastet";
        }

        if (analysis.MaximumUsedSpacePercent >= 95)
        {
            return "Fast vollständig belegt";
        }

        if (analysis.MaximumUsedSpacePercent >= 90)
        {
            return "Wenig freier Speicher";
        }

        if (analysis.MinimumRemainingLifePercent < 80)
        {
            return "Lebenszustand prüfen";
        }

        return "Keine starke Auffälligkeit";
    }

    private static string FormatTemperature(
        double? value)
    {
        return value is null
            ? "–"
            : $"{value.Value:F1} °C";
    }

    private static string FormatPercent(
        double? value)
    {
        return value is null
            ? "–"
            : $"{value.Value:F1} %";
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

    private static string JoinValues(
        string average,
        string maximum)
    {
        return $"{average} Ø / {maximum} max.";
    }
}