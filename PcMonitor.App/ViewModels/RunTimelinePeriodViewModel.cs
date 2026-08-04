using PcMonitor.Core.Diagnostics;
using PcMonitor.Core.Timeline;

namespace PcMonitor.App.ViewModels;

public sealed class RunTimelinePeriodViewModel
{
    public RunTimelinePeriodViewModel(
        RunTimelinePeriod period)
    {
        Period = period
            ?? throw new ArgumentNullException(nameof(period));
    }

    public RunTimelinePeriod Period { get; }

    public string TimeText
    {
        get
        {
            string start = Period.StartedAt.ToLocalTime().ToString("HH:mm:ss");
            string end = Period.EndedAt.ToLocalTime().ToString("HH:mm:ss");

            return Period.Duration.TotalSeconds <= 1
                ? start
                : $"{start}–{end}";
        }
    }

    public string DurationText => FormatDuration(Period.Duration);

    public string SeverityText =>
        Period.Severity switch
        {
            DiagnosticSeverity.Critical => "Kritisch",
            DiagnosticSeverity.Warning => "Warnung",
            DiagnosticSeverity.Information => "Hinweis",
            _ => Period.Severity.ToString()
        };

    public string CategoryText =>
        Period.Category switch
        {
            TimelineEventCategory.Cpu => "CPU",
            TimelineEventCategory.Gpu => "GPU",
            TimelineEventCategory.Memory => "RAM",
            TimelineEventCategory.Disk => "Datenträger",
            TimelineEventCategory.Network => "Netzwerk",
            TimelineEventCategory.Process => "Prozess",
            TimelineEventCategory.System => "Korrelation",
            _ => Period.Category.ToString()
        };

    public string KindText =>
        Period.IsCorrelation
            ? "Korrelation"
            : "Messwert";

    public string SourceText => Period.SourceName;
    public string TitleText => Period.Title;
    public string DescriptionText => Period.Description;

    public string EventCountText =>
        Period.EventCount == 1
            ? "1 Messpunkt"
            : $"{Period.EventCount} Messpunkte";

    public string AverageValueText =>
        FormatValue(Period.AverageNumericValue, Period.Unit);

    public string MaximumValueText =>
        FormatValue(Period.MaximumNumericValue, Period.Unit);

    private static string FormatValue(
        double? value,
        string? unit)
    {
        if (value is null)
        {
            return "–";
        }

        string formatted = value.Value.ToString("F1");

        return string.IsNullOrWhiteSpace(unit)
            ? formatted
            : $"{formatted} {unit}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 1)
        {
            return "< 1 s";
        }

        if (duration.TotalMinutes < 1)
        {
            return $"{duration.TotalSeconds:F0} s";
        }

        if (duration.TotalHours < 1)
        {
            return $"{(int)duration.TotalMinutes} min {duration.Seconds} s";
        }

        return $"{(int)duration.TotalHours} h {duration.Minutes} min";
    }
}
