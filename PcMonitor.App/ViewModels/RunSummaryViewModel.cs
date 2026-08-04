using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class RunSummaryViewModel
{
    public RunSummaryViewModel(
        RunSummary run)
    {
        Run = run
              ?? throw new ArgumentNullException(
                  nameof(run));
    }

    public RunSummary Run { get; }

    public Guid Id => Run.Id;

    public string Name => Run.Name;

    public string StartedAtText =>
        Run.StartedAt.LocalDateTime
            .ToString("dd.MM.yyyy HH:mm:ss");

    public string EndedAtText =>
        Run.EndedAt?.LocalDateTime
            .ToString("dd.MM.yyyy HH:mm:ss")
        ?? "Nicht beendet";

    public string DurationText =>
        FormatDuration(Run.Duration);

    public string StatusText =>
        Run.Status switch
        {
            RunStatus.Ready => "Bereit",
            RunStatus.Running => "Läuft",
            RunStatus.Completed => "Abgeschlossen",
            _ => Run.Status.ToString()
        };

    public string SampleCountText =>
        $"{Run.SampleCount:N0}";

    private static string FormatDuration(
        TimeSpan duration)
    {
        int totalHours =
            (int)duration.TotalHours;

        return
            $"{totalHours:00}:" +
            $"{duration.Minutes:00}:" +
            $"{duration.Seconds:00}";
    }
}