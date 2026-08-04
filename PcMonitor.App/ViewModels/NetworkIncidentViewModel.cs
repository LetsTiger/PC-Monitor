using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class NetworkIncidentViewModel
{
    public NetworkIncidentViewModel(
        NetworkIncident incident)
    {
        Incident = incident
                   ?? throw new ArgumentNullException(
                       nameof(incident));
    }

    public NetworkIncident Incident { get; }

    public string TargetText =>
        Incident.Target switch
        {
            NetworkIncidentTarget.Gateway =>
                "Standardgateway",

            NetworkIncidentTarget.Internet =>
                "Internet",

            _ => Incident.Target.ToString()
        };

    public string AddressText =>
        Incident.Address ?? "–";

    public string StartedAtText =>
        Incident.StartedAt.LocalDateTime
            .ToString("dd.MM.yyyy HH:mm:ss");

    public string DurationText =>
        Incident.Duration.TotalSeconds < 1
            ? "< 1 Sekunde"
            : $"{Incident.Duration.TotalSeconds:F1} Sekunden";

    public string FailureCountText =>
        Incident.FailureCount.ToString("N0");
}