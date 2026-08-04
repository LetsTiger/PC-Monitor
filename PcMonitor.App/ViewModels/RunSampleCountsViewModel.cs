using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class RunSampleCountsViewModel
{
    public RunSampleCountsViewModel(
        RunSampleCounts counts)
    {
        Counts = counts
            ?? throw new ArgumentNullException(
                nameof(counts));
    }

    public RunSampleCounts Counts { get; }

    public string TotalText =>
        Counts.TotalSamples.ToString("N0");

    public string SystemText =>
        Counts.SystemSamples.ToString("N0");

    public string NetworkText =>
        Counts.NetworkSamples.ToString("N0");

    public string HardwareText =>
        Counts.HardwareSamples.ToString("N0");

    public string CpuCoreText =>
        Counts.CpuCoreSamples.ToString("N0");

    public string DiskText =>
        Counts.DiskSamples.ToString("N0");

    public string ProcessText =>
        Counts.ProcessSamples.ToString("N0");

    public string NetworkIncidentsText =>
        Counts.NetworkIncidents.ToString("N0");

    public string StatusText =>
        CreateStatusText(Counts);

    private static string CreateStatusText(
        RunSampleCounts counts)
    {
        var missingTypes =
            new List<string>();

        if (counts.SystemSamples == 0)
        {
            missingTypes.Add("System");
        }

        if (counts.NetworkSamples == 0)
        {
            missingTypes.Add("Netzwerk");
        }

        if (counts.HardwareSamples == 0)
        {
            missingTypes.Add("Hardware");
        }

        if (counts.CpuCoreSamples == 0)
        {
            missingTypes.Add("CPU-Threads");
        }

        if (counts.DiskSamples == 0)
        {
            missingTypes.Add("Datenträger");
        }

        if (counts.ProcessSamples == 0)
        {
            missingTypes.Add("Prozesse");
        }

        if (missingTypes.Count == 0)
        {
            return
                "Alle erwarteten Sampletypen sind vorhanden.";
        }

        return
            "Keine Daten für: " +
            string.Join(", ", missingTypes);
    }
}