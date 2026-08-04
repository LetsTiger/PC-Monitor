using PcMonitor.Core.Diagnostics;
using PcMonitor.Core.Timeline;

namespace PcMonitor.App.ViewModels;

public sealed class RunTimelineEventViewModel
{
    public RunTimelineEventViewModel(
        RunTimelineEvent timelineEvent)
    {
        Event = timelineEvent
                ?? throw new ArgumentNullException(
                    nameof(timelineEvent));
    }

    public RunTimelineEvent Event { get; }

    public string TimeText =>
        Event.Timestamp
            .ToLocalTime()
            .ToString("HH:mm:ss");

    public string SeverityText =>
        Event.Severity switch
        {
            DiagnosticSeverity.Critical =>
                "Kritisch",

            DiagnosticSeverity.Warning =>
                "Warnung",

            DiagnosticSeverity.Information =>
                "Hinweis",

            _ => Event.Severity.ToString()
        };

    public string CategoryText =>
        Event.Category switch
        {
            TimelineEventCategory.Cpu =>
                "CPU",

            TimelineEventCategory.Gpu =>
                "GPU",

            TimelineEventCategory.Memory =>
                "RAM",

            TimelineEventCategory.Disk =>
                "Datenträger",

            TimelineEventCategory.Network =>
                "Netzwerk",

            TimelineEventCategory.Process =>
                "Prozess",

            TimelineEventCategory.System =>
                "System",

            _ => Event.Category.ToString()
        };

    public string TitleText =>
        Event.Title;

    public string DescriptionText =>
        Event.Description;

    public string ValueText
    {
        get
        {
            if (Event.NumericValue is null)
            {
                return "–";
            }

            string number =
                Event.NumericValue.Value.ToString("F1");

            return string.IsNullOrWhiteSpace(Event.Unit)
                ? number
                : $"{number} {Event.Unit}";
        }
    }
}