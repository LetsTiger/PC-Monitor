using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class RunAnalysisViewModel
{
    public RunAnalysisViewModel(
        RunAnalysis analysis)
    {
        Analysis = analysis
            ?? throw new ArgumentNullException(
                nameof(analysis));
    }

    public RunAnalysis Analysis { get; }

    public string AverageCpuText =>
        FormatPercent(
            Analysis.AverageCpuUsagePercent);

    public string MaximumCpuText =>
        FormatPercent(
            Analysis.MaximumCpuUsagePercent);

    public string AverageMemoryText =>
        FormatPercent(
            Analysis.AverageMemoryUsagePercent);

    public string MaximumMemoryText =>
        FormatPercent(
            Analysis.MaximumMemoryUsagePercent);

    public string MaximumUsedMemoryText =>
        Analysis.MaximumUsedMemoryBytes is null
            ? "–"
            : FormatBytes(
                Analysis.MaximumUsedMemoryBytes.Value);

    public string AverageGatewayLatencyText =>
        FormatMilliseconds(
            Analysis.AverageGatewayLatencyMilliseconds);

    public string MaximumGatewayLatencyText =>
        FormatMilliseconds(
            Analysis.MaximumGatewayLatencyMilliseconds);

    public string GatewayPacketLossText =>
        FormatPercent(
            Analysis.MaximumGatewayPacketLossPercent);

    public string GatewayJitterText =>
        FormatMilliseconds(
            Analysis.AverageGatewayJitterMilliseconds);

    public string GatewayFailuresText =>
        $"{Analysis.GatewayFailureCount:N0}";

    public string MaximumGatewayFailureSequenceText =>
        $"{Analysis.MaximumGatewayConsecutiveFailures:N0}";

    public string AverageInternetLatencyText =>
        FormatMilliseconds(
            Analysis.AverageInternetLatencyMilliseconds);

    public string MaximumInternetLatencyText =>
        FormatMilliseconds(
            Analysis.MaximumInternetLatencyMilliseconds);

    public string InternetPacketLossText =>
        FormatPercent(
            Analysis.MaximumInternetPacketLossPercent);

    public string InternetJitterText =>
        FormatMilliseconds(
            Analysis.AverageInternetJitterMilliseconds);

    public string InternetFailuresText =>
        $"{Analysis.InternetFailureCount:N0}";

    public string MaximumInternetFailureSequenceText =>
        $"{Analysis.MaximumInternetConsecutiveFailures:N0}";

    public string AssessmentText =>
        CreateAssessment(Analysis);
    
    public string CpuNameText =>
    Analysis.CpuName ?? "–";

    public string CpuAverageTemperatureText =>
        FormatTemperature(
            Analysis.AverageCpuTemperatureCelsius);

    public string CpuMaximumTemperatureText =>
        FormatTemperature(
            Analysis.MaximumCpuTemperatureCelsius);

    public string CpuMaximumCoreTemperatureText =>
        FormatTemperature(
            Analysis.MaximumCpuCoreTemperatureCelsius);

    public string CpuAverageClockText =>
        FormatClock(
            Analysis.AverageCpuClockMegahertz);

    public string CpuMinimumClockText =>
        FormatClock(
            Analysis.MinimumCpuClockMegahertz);

    public string CpuMaximumClockText =>
        FormatClock(
            Analysis.MaximumCpuClockMegahertz);

    public string CpuAveragePowerText =>
        FormatWatts(
            Analysis.AverageCpuPackagePowerWatts);

    public string CpuMaximumPowerText =>
        FormatWatts(
            Analysis.MaximumCpuPackagePowerWatts);

    public string GpuNameText =>
        Analysis.GpuName ?? "–";

    public string GpuAverageLoadText =>
        FormatPercent(
            Analysis.AverageGpuLoadPercent);

    public string GpuMaximumLoadText =>
        FormatPercent(
            Analysis.MaximumGpuLoadPercent);

    public string GpuAverageTemperatureText =>
        FormatTemperature(
            Analysis.AverageGpuTemperatureCelsius);

    public string GpuMaximumTemperatureText =>
        FormatTemperature(
            Analysis.MaximumGpuTemperatureCelsius);

    public string GpuMaximumHotspotText =>
        FormatTemperature(
            Analysis.MaximumGpuHotspotTemperatureCelsius);

    public string GpuAverageClockText =>
        FormatClock(
            Analysis.AverageGpuCoreClockMegahertz);

    public string GpuMinimumClockText =>
        FormatClock(
            Analysis.MinimumGpuCoreClockMegahertz);

    public string GpuMaximumClockText =>
        FormatClock(
            Analysis.MaximumGpuCoreClockMegahertz);

    public string GpuAveragePowerText =>
        FormatWatts(
            Analysis.AverageGpuPowerWatts);

    public string GpuMaximumPowerText =>
        FormatWatts(
            Analysis.MaximumGpuPowerWatts);

    public string GpuMaximumMemoryText =>
        FormatGpuMemory(
            Analysis.MaximumGpuUsedMemoryMegabytes,
            Analysis.GpuTotalMemoryMegabytes);

    private static string FormatPercent(
        double? value)
    {
        return value is null
            ? "–"
            : $"{value.Value:F1} %";
    }

    private static string FormatMilliseconds(
        double? value)
    {
        return value is null
            ? "–"
            : $"{value.Value:F1} ms";
    }

    private static string FormatMilliseconds(
        long? value)
    {
        return value is null
            ? "–"
            : $"{value.Value} ms";
    }

    private static string FormatBytes(
        ulong bytes)
    {
        const double gibibyte =
            1024d * 1024d * 1024d;

        return $"{bytes / gibibyte:F1} GB";
    }
    
    
    
    private static string FormatTemperature(
        double? value)
    {
        return value is null
            ? "–"
            : $"{value.Value:F1} °C";
    }

    private static string FormatClock(
        double? megahertz)
    {
        if (megahertz is null)
        {
            return "–";
        }

        return megahertz.Value >= 1_000
            ? $"{megahertz.Value / 1_000:F2} GHz"
            : $"{megahertz.Value:F0} MHz";
    }

    private static string FormatWatts(
        double? value)
    {
        return value is null
            ? "–"
            : $"{value.Value:F1} W";
    }

    private static string FormatGpuMemory(
        double? usedMegabytes,
        double? totalMegabytes)
    {
        if (usedMegabytes is null)
        {
            return "–";
        }

        if (totalMegabytes is null)
        {
            return
                $"{usedMegabytes.Value / 1024:F1} GB";
        }

        return
            $"{usedMegabytes.Value / 1024:F1} / " +
            $"{totalMegabytes.Value / 1024:F1} GB";
    }

    private static string CreateAssessment(
        RunAnalysis analysis)
    {
        if (analysis.MaximumGatewayConsecutiveFailures >= 3)
        {
            return
                "Mehrere aufeinanderfolgende Ausfälle zum " +
                "Standardgateway wurden erkannt. Das deutet " +
                "auf ein lokales Netzwerk-, WLAN- oder " +
                "Routerproblem hin.";
        }

        if (analysis.MaximumInternetConsecutiveFailures >= 3)
        {
            return
                "Das Standardgateway war erreichbar, aber die " +
                "Internetverbindung ist mehrfach hintereinander " +
                "ausgefallen.";
        }

        if (analysis.InternetFailureCount > 0)
        {
            return
                "Während des Runs wurden einzelne fehlgeschlagene " +
                "Internetmessungen erkannt.";
        }

        if (analysis.MaximumInternetPacketLossPercent >= 5)
        {
            return
                "Der gleitende Paketverlust zum Internetziel war " +
                "zeitweise erhöht.";
        }

        if (analysis.AverageInternetJitterMilliseconds >= 20)
        {
            return
                "Die Internetlatenz schwankte deutlich. Das kann " +
                "bei Spielen als ungleichmäßiger Ping auffallen.";
        }
        
        if (analysis.MaximumCpuCoreTemperatureCelsius >= 95 ||
            analysis.MaximumCpuTemperatureCelsius >= 95)
        {
            return
                "Die CPU erreichte mindestens 95 °C. " +
                "Thermische Drosselung oder ein Kühlungsproblem " +
                "sollten genauer geprüft werden.";
        }

        if (analysis.MaximumCpuCoreTemperatureCelsius >= 90 ||
            analysis.MaximumCpuTemperatureCelsius >= 90)
        {
            return
                "Die CPU-Temperatur war zeitweise sehr hoch. " +
                "Prüfe Kühlung, Lüfter, Wärmeleitpaste und " +
                "mögliche Taktverluste während dieser Zeit.";
        }

        if (analysis.MaximumGpuHotspotTemperatureCelsius >= 100)
        {
            return
                "Der GPU-Hotspot erreichte mindestens 100 °C. " +
                "Die GPU-Kühlung sollte genauer geprüft werden.";
        }

        if (analysis.MaximumGpuTemperatureCelsius >= 85)
        {
            return
                "Die GPU-Temperatur war sehr hoch. " +
                "Prüfe Luftstrom, Lüfter und mögliche Drosselung.";
        }

        if (analysis.MaximumGpuLoadPercent >= 97 &&
            analysis.MaximumCpuUsagePercent < 90)
        {
            return
                "Die GPU war nahezu vollständig ausgelastet, " +
                "während die gesamte CPU-Auslastung darunter lag. " +
                "Das deutet eher auf ein GPU-Limit hin.";
        }

        if (analysis.MaximumCpuUsagePercent >= 95 &&
            analysis.MaximumGpuLoadPercent < 90)
        {
            return
                "Die CPU erreichte eine sehr hohe Auslastung, " +
                "während die GPU nicht vollständig ausgelastet war. " +
                "Das kann auf ein CPU-Limit hindeuten; für eine " +
                "sichere Bewertung benötigen wir zusätzlich " +
                "Auslastung pro CPU-Kern und Frametime-Daten.";
        }

        if (analysis.MaximumCpuUsagePercent >= 95)
        {
            return
                "Die gesamte CPU-Auslastung erreichte mindestens " +
                "95 %. Für eine echte Bottleneck-Bewertung benötigen " +
                "wir später zusätzlich Werte pro CPU-Kern und GPU.";
        }

        if (analysis.MaximumMemoryUsagePercent >= 90)
        {
            return
                "Die Arbeitsspeicherauslastung war sehr hoch. " +
                "Auslagerungsaktivität könnte dadurch wahrscheinlicher sein.";
        }

        return
            "In den aktuell erfassten System- und Netzwerkdaten " +
            "wurde keine eindeutige starke Auffälligkeit erkannt.";
    }
}