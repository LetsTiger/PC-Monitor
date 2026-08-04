using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class TimelineStoryViewModel
{
    public TimelineStoryViewModel(
        TimelineStory story)
    {
        Story = story;
    }

    public TimelineStory Story { get; }

    public string TimeText =>
        Story.StartedAt.LocalDateTime
            .ToString("HH:mm:ss.fff");

    public string DurationText =>
        Story.Duration.TotalSeconds < 1
            ? $"{Story.Duration.TotalMilliseconds:F0} ms"
            : $"{Story.Duration.TotalSeconds:F2} s";

    public string ConfidenceText =>
        $"{Story.ConfidencePercent:F0} %";

    public string TitleText => Story.Title;
    public string CauseText => Story.Cause;
    public string ImpactText => Story.Impact;
    public string RecoveryText => Story.Recovery;
}
