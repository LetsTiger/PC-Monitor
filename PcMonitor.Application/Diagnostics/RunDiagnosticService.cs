using PcMonitor.Core.Diagnostics;
using PcMonitor.Core.Models;

namespace PcMonitor.Application.Diagnostics;

public sealed class RunDiagnosticService :
    IRunDiagnosticService
{
    public IReadOnlyList<DiagnosticFinding> Analyze(
        RunDiagnosticContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var findings =
            new List<DiagnosticFinding>();

        AnalyzeCpu(
            context.RunAnalysis,
            context.CpuCoreAnalyses,
            findings);

        AnalyzeGpu(
            context.RunAnalysis,
            findings);

        AnalyzeMemory(
            context.RunAnalysis,
            findings);

        AnalyzeNetwork(
            context.RunAnalysis,
            context.NetworkIncidents,
            findings);

        AnalyzeDisks(
            context.DiskAnalyses,
            findings);

        AnalyzeProcesses(
            context.ProcessAnalyses,
            findings);

        AnalyzePerformance(
            context.FrameAnalyses,
            context.BottleneckAnalysis,
            findings);

        if (findings.Count == 0)
        {
            findings.Add(
                new DiagnosticFinding(
                    Code: "RUN_NO_MAJOR_FINDINGS",
                    Severity:
                        DiagnosticSeverity.Information,
                    Category:
                        DiagnosticCategory.System,
                    Title:
                        "Keine starke Auffälligkeit erkannt",
                    Description:
                        "Die aktuell erfassten Messwerte enthalten " +
                        "keinen eindeutigen kritischen Engpass oder Ausfall.",
                    Recommendation:
                        "Für Mikroruckler werden später zusätzlich " +
                        "Frametime- und FPS-Daten hilfreich sein."));
        }

        return findings
            .OrderByDescending(finding =>
                finding.Severity)
            .ThenBy(finding =>
                finding.Category)
            .ThenBy(finding =>
                finding.Title)
            .ToArray();
    }

    private static void AnalyzeCpu(
        RunAnalysis? analysis,
        IReadOnlyList<CpuCoreRunAnalysis> coreAnalyses,
        ICollection<DiagnosticFinding> findings)
    {
        if (analysis is not null)
        {
            double? maximumTemperature =
                MaxNullable(
                    analysis.MaximumCpuTemperatureCelsius,
                    analysis.MaximumCpuCoreTemperatureCelsius);

            if (maximumTemperature >= 95)
            {
                findings.Add(
                    new DiagnosticFinding(
                        Code: "CPU_TEMPERATURE_CRITICAL",
                        Severity:
                            DiagnosticSeverity.Critical,
                        Category:
                            DiagnosticCategory.Cpu,
                        Title:
                            "CPU-Temperatur kritisch hoch",
                        Description:
                            $"Die CPU erreichte maximal " +
                            $"{maximumTemperature.Value:F1} °C.",
                        Recommendation:
                            "Kühler, Lüfter, Luftstrom, Wärmeleitpaste " +
                            "und mögliche thermische Drosselung prüfen."));
            }
            else if (maximumTemperature >= 90)
            {
                findings.Add(
                    new DiagnosticFinding(
                        Code: "CPU_TEMPERATURE_HIGH",
                        Severity:
                            DiagnosticSeverity.Warning,
                        Category:
                            DiagnosticCategory.Cpu,
                        Title:
                            "CPU-Temperatur sehr hoch",
                        Description:
                            $"Die CPU erreichte maximal " +
                            $"{maximumTemperature.Value:F1} °C.",
                        Recommendation:
                            "Prüfen, ob der CPU-Takt bei diesen " +
                            "Temperaturen sichtbar absinkt."));
            }

            if (analysis.MaximumCpuUsagePercent >= 95)
            {
                findings.Add(
                    new DiagnosticFinding(
                        Code: "CPU_TOTAL_LOAD_HIGH",
                        Severity:
                            DiagnosticSeverity.Warning,
                        Category:
                            DiagnosticCategory.Cpu,
                        Title:
                            "Sehr hohe CPU-Gesamtauslastung",
                        Description:
                            $"Die CPU-Gesamtauslastung erreichte " +
                            $"{analysis.MaximumCpuUsagePercent.Value:F1} %.",
                        Recommendation:
                            "Parallel laufende Prozesse und die " +
                            "Auslastung einzelner CPU-Threads prüfen."));
            }
        }

        CpuCoreRunAnalysis? sustainedThread =
            coreAnalyses
                .Where(core =>
                    core.ShareAbove95Percent >= 20)
                .OrderByDescending(core =>
                    core.ShareAbove95Percent)
                .FirstOrDefault();

        if (sustainedThread is not null)
        {
            findings.Add(
                new DiagnosticFinding(
                    Code: "CPU_THREAD_SUSTAINED_LIMIT",
                    Severity:
                        DiagnosticSeverity.Critical,
                    Category:
                        DiagnosticCategory.Cpu,
                    Title:
                        "Ein CPU-Thread war dauerhaft am Limit",
                    Description:
                        $"Core {sustainedThread.CoreIndex}, " +
                        $"Thread {sustainedThread.ThreadIndex} war " +
                        $"{sustainedThread.ShareAbove95Percent:F1} % " +
                        "der Messungen mindestens zu 95 % ausgelastet.",
                    Recommendation:
                        "Das kann auf ein Main-Thread- oder " +
                        "Single-Core-Limit des Spiels hindeuten."));
        }
        else
        {
            CpuCoreRunAnalysis? frequentlyLoadedThread =
                coreAnalyses
                    .Where(core =>
                        core.ShareAbove90Percent >= 10)
                    .OrderByDescending(core =>
                        core.ShareAbove90Percent)
                    .FirstOrDefault();

            if (frequentlyLoadedThread is not null)
            {
                findings.Add(
                    new DiagnosticFinding(
                        Code: "CPU_THREAD_FREQUENT_HIGH_LOAD",
                        Severity:
                            DiagnosticSeverity.Warning,
                        Category:
                            DiagnosticCategory.Cpu,
                        Title:
                            "Ein CPU-Thread war häufig stark ausgelastet",
                        Description:
                            $"Core {frequentlyLoadedThread.CoreIndex}, " +
                            $"Thread {frequentlyLoadedThread.ThreadIndex} " +
                            $"war {frequentlyLoadedThread.ShareAbove90Percent:F1} % " +
                            "der Messungen über 90 % ausgelastet.",
                        Recommendation:
                            "Mit GPU-Auslastung und späteren " +
                            "Frametime-Spitzen vergleichen."));
            }
        }
    }

    private static void AnalyzeGpu(
        RunAnalysis? analysis,
        ICollection<DiagnosticFinding> findings)
    {
        if (analysis is null)
        {
            return;
        }

        if (analysis.MaximumGpuHotspotTemperatureCelsius >= 105)
        {
            findings.Add(
                new DiagnosticFinding(
                    Code: "GPU_HOTSPOT_CRITICAL",
                    Severity:
                        DiagnosticSeverity.Critical,
                    Category:
                        DiagnosticCategory.Gpu,
                    Title:
                        "GPU-Hotspot kritisch hoch",
                    Description:
                        $"Der GPU-Hotspot erreichte " +
                        $"{analysis.MaximumGpuHotspotTemperatureCelsius.Value:F1} °C.",
                    Recommendation:
                        "GPU-Kühlung, Lüfterkurve und Gehäuse-Luftstrom prüfen."));
        }
        else if (analysis.MaximumGpuHotspotTemperatureCelsius >= 95)
        {
            findings.Add(
                new DiagnosticFinding(
                    Code: "GPU_HOTSPOT_HIGH",
                    Severity:
                        DiagnosticSeverity.Warning,
                    Category:
                        DiagnosticCategory.Gpu,
                    Title:
                        "GPU-Hotspot erhöht",
                    Description:
                        $"Der GPU-Hotspot erreichte " +
                        $"{analysis.MaximumGpuHotspotTemperatureCelsius.Value:F1} °C.",
                    Recommendation:
                        "Temperaturdifferenz zwischen GPU-Core und Hotspot beobachten."));
        }

        if (analysis.MaximumGpuTemperatureCelsius >= 85)
        {
            findings.Add(
                new DiagnosticFinding(
                    Code: "GPU_TEMPERATURE_HIGH",
                    Severity:
                        DiagnosticSeverity.Warning,
                    Category:
                        DiagnosticCategory.Gpu,
                    Title:
                        "GPU-Temperatur sehr hoch",
                    Description:
                        $"Die GPU erreichte " +
                        $"{analysis.MaximumGpuTemperatureCelsius.Value:F1} °C.",
                    Recommendation:
                        "Luftstrom und Lüfterdrehzahl prüfen."));
        }

        if (analysis.AverageGpuLoadPercent >= 95)
        {
            findings.Add(
                new DiagnosticFinding(
                    Code: "GPU_SUSTAINED_LIMIT",
                    Severity:
                        DiagnosticSeverity.Information,
                    Category:
                        DiagnosticCategory.Gpu,
                    Title:
                        "GPU war nahezu dauerhaft ausgelastet",
                    Description:
                        $"Die durchschnittliche GPU-Auslastung betrug " +
                        $"{analysis.AverageGpuLoadPercent.Value:F1} %.",
                    Recommendation:
                        "Das spricht eher für ein GPU-Limit als für ein CPU-Limit."));
        }
        else if (analysis.MaximumGpuLoadPercent >= 98)
        {
            findings.Add(
                new DiagnosticFinding(
                    Code: "GPU_PEAK_LIMIT",
                    Severity:
                        DiagnosticSeverity.Information,
                    Category:
                        DiagnosticCategory.Gpu,
                    Title:
                        "GPU erreichte kurzzeitig Volllast",
                    Description:
                        $"Die maximale GPU-Auslastung betrug " +
                        $"{analysis.MaximumGpuLoadPercent.Value:F1} %.",
                    Recommendation:
                        "Ohne Frametime-Daten ist noch nicht erkennbar, " +
                        "ob diese Spitze ein Problem verursachte."));
        }

        if (analysis.MaximumGpuUsedMemoryMegabytes is not null &&
            analysis.GpuTotalMemoryMegabytes is > 0)
        {
            double vramShare =
                analysis.MaximumGpuUsedMemoryMegabytes.Value /
                analysis.GpuTotalMemoryMegabytes.Value *
                100.0;

            if (vramShare >= 95)
            {
                findings.Add(
                    new DiagnosticFinding(
                        Code: "GPU_VRAM_CRITICAL",
                        Severity:
                            DiagnosticSeverity.Critical,
                        Category:
                            DiagnosticCategory.Gpu,
                        Title:
                            "Grafikspeicher nahezu vollständig belegt",
                        Description:
                            $"Maximal waren {vramShare:F1} % des VRAM belegt.",
                        Recommendation:
                            "Texturqualität reduzieren und auf " +
                            "Nachladeruckler achten."));
            }
            else if (vramShare >= 90)
            {
                findings.Add(
                    new DiagnosticFinding(
                        Code: "GPU_VRAM_HIGH",
                        Severity:
                            DiagnosticSeverity.Warning,
                        Category:
                            DiagnosticCategory.Gpu,
                        Title:
                            "Hohe VRAM-Belegung",
                        Description:
                            $"Maximal waren {vramShare:F1} % des VRAM belegt.",
                        Recommendation:
                            "Hohe Textur- oder Auflösungseinstellungen prüfen."));
            }
        }
    }

    private static void AnalyzeMemory(
        RunAnalysis? analysis,
        ICollection<DiagnosticFinding> findings)
    {
        if (analysis?.MaximumMemoryUsagePercent >= 95)
        {
            findings.Add(
                new DiagnosticFinding(
                    Code: "MEMORY_USAGE_CRITICAL",
                    Severity:
                        DiagnosticSeverity.Critical,
                    Category:
                        DiagnosticCategory.Memory,
                    Title:
                        "Arbeitsspeicher nahezu vollständig belegt",
                    Description:
                        $"Die RAM-Auslastung erreichte " +
                        $"{analysis.MaximumMemoryUsagePercent.Value:F1} %.",
                    Recommendation:
                        "Speicherintensive Hintergrundprozesse und " +
                        "Auslagerungsaktivität prüfen."));
        }
        else if (analysis?.MaximumMemoryUsagePercent >= 90)
        {
            findings.Add(
                new DiagnosticFinding(
                    Code: "MEMORY_USAGE_HIGH",
                    Severity:
                        DiagnosticSeverity.Warning,
                    Category:
                        DiagnosticCategory.Memory,
                    Title:
                        "Hohe Arbeitsspeicherauslastung",
                    Description:
                        $"Die RAM-Auslastung erreichte " +
                        $"{analysis.MaximumMemoryUsagePercent.Value:F1} %.",
                    Recommendation:
                        "Nicht benötigte Anwendungen schließen und " +
                        "den Speicherverbrauch des Spiels prüfen."));
        }
    }

    private static void AnalyzeNetwork(
        RunAnalysis? analysis,
        IReadOnlyList<NetworkIncident> incidents,
        ICollection<DiagnosticFinding> findings)
    {
        int gatewayIncidentCount =
            incidents.Count(incident =>
                incident.Target ==
                NetworkIncidentTarget.Gateway);

        int internetIncidentCount =
            incidents.Count(incident =>
                incident.Target ==
                NetworkIncidentTarget.Internet);

        if (gatewayIncidentCount > 0)
        {
            findings.Add(
                new DiagnosticFinding(
                    Code: "NETWORK_GATEWAY_INCIDENTS",
                    Severity:
                        DiagnosticSeverity.Critical,
                    Category:
                        DiagnosticCategory.Network,
                    Title:
                        "Lokale Netzwerkabbrüche erkannt",
                    Description:
                        $"{gatewayIncidentCount} Vorfall/Vorfälle zum " +
                        "Standardgateway wurden gespeichert.",
                    Recommendation:
                        "LAN-Kabel, WLAN-Verbindung, Netzwerkadapter " +
                        "und Router prüfen."));
        }
        else if (internetIncidentCount > 0)
        {
            findings.Add(
                new DiagnosticFinding(
                    Code: "NETWORK_INTERNET_INCIDENTS",
                    Severity:
                        DiagnosticSeverity.Warning,
                    Category:
                        DiagnosticCategory.Network,
                    Title:
                        "Internetabbrüche erkannt",
                    Description:
                        $"{internetIncidentCount} Internetvorfall/Vorfälle " +
                        "wurden gespeichert, während das lokale Gateway " +
                        "nicht zwingend betroffen war.",
                    Recommendation:
                        "Provider-Verbindung, Modem und Router-Protokolle prüfen."));
        }

        if (analysis?.MaximumInternetPacketLossPercent >= 5)
        {
            findings.Add(
                new DiagnosticFinding(
                    Code: "NETWORK_PACKET_LOSS_HIGH",
                    Severity:
                        DiagnosticSeverity.Warning,
                    Category:
                        DiagnosticCategory.Network,
                    Title:
                        "Erhöhter Paketverlust",
                    Description:
                        $"Der maximale gleitende Paketverlust betrug " +
                        $"{analysis.MaximumInternetPacketLossPercent.Value:F1} %.",
                    Recommendation:
                        "Mit Gateway-Paketverlust vergleichen, um lokales " +
                        "Netzwerk und Internetverbindung zu unterscheiden."));
        }

        if (analysis?.AverageInternetJitterMilliseconds >= 20)
        {
            findings.Add(
                new DiagnosticFinding(
                    Code: "NETWORK_JITTER_HIGH",
                    Severity:
                        DiagnosticSeverity.Warning,
                    Category:
                        DiagnosticCategory.Network,
                    Title:
                        "Hoher Netzwerk-Jitter",
                    Description:
                        $"Der durchschnittliche Internet-Jitter betrug " +
                        $"{analysis.AverageInternetJitterMilliseconds.Value:F1} ms.",
                    Recommendation:
                        "Parallele Uploads, WLAN-Störungen und " +
                        "Bufferbloat prüfen."));
        }

        if (analysis?.MaximumInternetLatencyMilliseconds >= 150)
        {
            findings.Add(
                new DiagnosticFinding(
                    Code: "NETWORK_LATENCY_SPIKE",
                    Severity:
                        DiagnosticSeverity.Warning,
                    Category:
                        DiagnosticCategory.Network,
                    Title:
                        "Hohe Latenzspitze",
                    Description:
                        $"Die maximale Internetlatenz betrug " +
                        $"{analysis.MaximumInternetLatencyMilliseconds.Value} ms.",
                    Recommendation:
                        "Zeitpunkt später mit Prozess-, Datenträger- " +
                        "und Frametime-Daten vergleichen."));
        }
    }

    private static void AnalyzeDisks(
        IReadOnlyList<DiskRunAnalysis> analyses,
        ICollection<DiagnosticFinding> findings)
    {
        foreach (DiskRunAnalysis disk in analyses)
        {
            if (disk.MaximumTemperatureCelsius >= 80)
            {
                findings.Add(
                    new DiagnosticFinding(
                        Code:
                            $"DISK_TEMPERATURE_CRITICAL:{disk.DeviceId}",
                        Severity:
                            DiagnosticSeverity.Critical,
                        Category:
                            DiagnosticCategory.Disk,
                        Title:
                            $"Datenträger sehr heiß: {disk.DeviceName}",
                        Description:
                            $"Die maximale Temperatur betrug " +
                            $"{disk.MaximumTemperatureCelsius.Value:F1} °C.",
                        Recommendation:
                            "Kühlung, Luftstrom oder NVMe-Kühlkörper prüfen."));
            }
            else if (disk.MaximumTemperatureCelsius >= 70)
            {
                findings.Add(
                    new DiagnosticFinding(
                        Code:
                            $"DISK_TEMPERATURE_HIGH:{disk.DeviceId}",
                        Severity:
                            DiagnosticSeverity.Warning,
                        Category:
                            DiagnosticCategory.Disk,
                        Title:
                            $"Datenträgertemperatur erhöht: {disk.DeviceName}",
                        Description:
                            $"Die maximale Temperatur betrug " +
                            $"{disk.MaximumTemperatureCelsius.Value:F1} °C.",
                        Recommendation:
                            "Auf mögliches thermisches Drosseln achten."));
            }

            if (disk.MaximumUsedSpacePercent >= 95)
            {
                findings.Add(
                    new DiagnosticFinding(
                        Code:
                            $"DISK_SPACE_CRITICAL:{disk.DeviceId}",
                        Severity:
                            DiagnosticSeverity.Critical,
                        Category:
                            DiagnosticCategory.Disk,
                        Title:
                            $"Datenträger fast voll: {disk.DeviceName}",
                        Description:
                            $"Der Datenträger war zu " +
                            $"{disk.MaximumUsedSpacePercent.Value:F1} % belegt.",
                        Recommendation:
                            "Speicherplatz freigeben; sehr wenig freier " +
                            "Speicher kann Updates und temporäre Dateien behindern."));
            }
            else if (disk.MaximumUsedSpacePercent >= 90)
            {
                findings.Add(
                    new DiagnosticFinding(
                        Code:
                            $"DISK_SPACE_HIGH:{disk.DeviceId}",
                        Severity:
                            DiagnosticSeverity.Warning,
                        Category:
                            DiagnosticCategory.Disk,
                        Title:
                            $"Wenig freier Speicher: {disk.DeviceName}",
                        Description:
                            $"Der Datenträger war zu " +
                            $"{disk.MaximumUsedSpacePercent.Value:F1} % belegt.",
                        Recommendation:
                            "Nicht benötigte Dateien entfernen und " +
                            "ausreichend Reserve freihalten."));
            }

            if (disk.MinimumRemainingLifePercent < 80)
            {
                findings.Add(
                    new DiagnosticFinding(
                        Code:
                            $"DISK_HEALTH_LOW:{disk.DeviceId}",
                        Severity:
                            DiagnosticSeverity.Warning,
                        Category:
                            DiagnosticCategory.Disk,
                        Title:
                            $"Datenträgerzustand prüfen: {disk.DeviceName}",
                        Description:
                            $"Der gemeldete Lebenszustand betrug mindestens " +
                            $"{disk.MinimumRemainingLifePercent.Value:F0} %.",
                        Recommendation:
                            "SMART-Werte prüfen und wichtige Daten sichern."));
            }

            if (disk.MaximumTotalActivityPercent >= 95)
            {
                findings.Add(
                    new DiagnosticFinding(
                        Code:
                            $"DISK_ACTIVITY_HIGH:{disk.DeviceId}",
                        Severity:
                            DiagnosticSeverity.Information,
                        Category:
                            DiagnosticCategory.Disk,
                        Title:
                            $"Datenträger erreichte Volllast: {disk.DeviceName}",
                        Description:
                            $"Die maximale Aktivität betrug " +
                            $"{disk.MaximumTotalActivityPercent.Value:F1} %.",
                        Recommendation:
                            "Eine einzelne Spitze ist noch kein Problem. " +
                            "Später mit Frametime-Einbrüchen vergleichen."));
            }
        }
    }

    private static void AnalyzeProcesses(
        IReadOnlyList<ProcessRunAnalysis> analyses,
        ICollection<DiagnosticFinding> findings)
    {
        ProcessRunAnalysis? highBackgroundCpu =
            analyses
                .Where(process =>
                    process.ForegroundSampleCount == 0 &&
                    process.MaximumCpuUsagePercent >= 40)
                .OrderByDescending(process =>
                    process.MaximumCpuUsagePercent)
                .FirstOrDefault();

        if (highBackgroundCpu is not null)
        {
            findings.Add(
                new DiagnosticFinding(
                    Code:
                        $"PROCESS_BACKGROUND_CPU:{highBackgroundCpu.ProcessName}",
                    Severity:
                        DiagnosticSeverity.Warning,
                    Category:
                        DiagnosticCategory.Process,
                    Title:
                        "Hohe CPU-Last durch Hintergrundprozess",
                    Description:
                        $"{highBackgroundCpu.ProcessName} erreichte " +
                        $"{highBackgroundCpu.MaximumCpuUsagePercent:F1} % CPU, " +
                        "ohne als Vordergrundprozess erkannt zu werden.",
                    Recommendation:
                        "Prüfen, ob dieser Prozess während des Spielens " +
                        "benötigt wird."));
        }

        ProcessRunAnalysis? highBackgroundWrite =
            analyses
                .Where(process =>
                    process.ForegroundSampleCount == 0 &&
                    process.MaximumWriteBytesPerSecond >=
                    100d * 1024d * 1024d)
                .OrderByDescending(process =>
                    process.MaximumWriteBytesPerSecond)
                .FirstOrDefault();

        if (highBackgroundWrite is not null)
        {
            findings.Add(
                new DiagnosticFinding(
                    Code:
                        $"PROCESS_BACKGROUND_WRITE:{highBackgroundWrite.ProcessName}",
                    Severity:
                        DiagnosticSeverity.Warning,
                    Category:
                        DiagnosticCategory.Process,
                    Title:
                        "Starke Schreibaktivität im Hintergrund",
                    Description:
                        $"{highBackgroundWrite.ProcessName} schrieb mit bis zu " +
                        $"{FormatRate(highBackgroundWrite.MaximumWriteBytesPerSecond)}.",
                    Recommendation:
                        "Downloads, Aufnahmen, Synchronisierung oder " +
                        "Antiviren-Scans prüfen."));
        }

        ProcessRunAnalysis? highMemoryProcess =
            analyses
                .Where(process =>
                    process.MaximumWorkingSetBytes >=
                    8L * 1024L * 1024L * 1024L)
                .OrderByDescending(process =>
                    process.MaximumWorkingSetBytes)
                .FirstOrDefault();

        if (highMemoryProcess is not null)
        {
            findings.Add(
                new DiagnosticFinding(
                    Code:
                        $"PROCESS_MEMORY_HIGH:{highMemoryProcess.ProcessName}",
                    Severity:
                        DiagnosticSeverity.Information,
                    Category:
                        DiagnosticCategory.Process,
                    Title:
                        "Sehr hoher Speicherverbrauch eines Prozesses",
                    Description:
                        $"{highMemoryProcess.ProcessName} verwendete maximal " +
                        $"{FormatBytes(highMemoryProcess.MaximumWorkingSetBytes)} RAM.",
                    Recommendation:
                        "Nur problematisch, wenn gleichzeitig der gesamte " +
                        "Arbeitsspeicher knapp wurde."));
        }
    }


    private static void AnalyzePerformance(
        IReadOnlyList<FrameRunAnalysis> frameAnalyses,
        BottleneckAnalysis? bottleneck,
        ICollection<DiagnosticFinding> findings)
    {
        FrameRunAnalysis? game =
            frameAnalyses.FirstOrDefault(item =>
                item.IsLikelyGame)
            ?? frameAnalyses.FirstOrDefault();

        if (game is not null)
        {
            if (game.FrameSpikesAbove100Milliseconds > 0)
            {
                findings.Add(
                    new DiagnosticFinding(
                        Code: "FRAME_SPIKES_CRITICAL",
                        Severity:
                            DiagnosticSeverity.Critical,
                        Category:
                            DiagnosticCategory.System,
                        Title:
                            "Sehr starke Frametime-Spitzen erkannt",
                        Description:
                            $"{game.FrameSpikesAbove100Milliseconds:N0} Frames " +
                            "überschritten 100 ms Frametime.",
                        Recommendation:
                            "Timeline 2.0 öffnen und die korrelierte Ursache " +
                            "jedes starken Spikes prüfen."));
            }
            else if (game.FrameSpikesAbove50Milliseconds > 0)
            {
                findings.Add(
                    new DiagnosticFinding(
                        Code: "FRAME_SPIKES_HIGH",
                        Severity:
                            DiagnosticSeverity.Warning,
                        Category:
                            DiagnosticCategory.System,
                        Title:
                            "Starke Frametime-Spitzen erkannt",
                        Description:
                            $"{game.FrameSpikesAbove50Milliseconds:N0} Frames " +
                            "überschritten 50 ms Frametime.",
                        Recommendation:
                            "Die Spike-Zeitpunkte mit CPU, GPU und Datenträger " +
                            "in der Timeline vergleichen."));
            }
        }

        if (bottleneck is null ||
            bottleneck.PrimaryType ==
            BottleneckType.Unknown)
        {
            return;
        }

        findings.Add(
            new DiagnosticFinding(
                Code:
                    $"BOTTLENECK_{bottleneck.PrimaryType}",
                Severity:
                    bottleneck.ConfidencePercent >= 80
                        ? DiagnosticSeverity.Warning
                        : DiagnosticSeverity.Information,
                Category:
                    DiagnosticCategory.System,
                Title:
                    bottleneck.Title,
                Description:
                    bottleneck.Description,
                Recommendation:
                    bottleneck.Recommendation));
    }

    private static double? MaxNullable(
        double? first,
        double? second)
    {
        if (first is null)
        {
            return second;
        }

        if (second is null)
        {
            return first;
        }

        return Math.Max(
            first.Value,
            second.Value);
    }

    private static string FormatRate(
        double? bytesPerSecond)
    {
        if (bytesPerSecond is null)
        {
            return "unbekannter Geschwindigkeit";
        }

        if (bytesPerSecond >= 1024d * 1024d * 1024d)
        {
            return
                $"{bytesPerSecond.Value /
                   (1024d * 1024d * 1024d):F2} GB/s";
        }

        if (bytesPerSecond >= 1024d * 1024d)
        {
            return
                $"{bytesPerSecond.Value /
                   (1024d * 1024d):F1} MB/s";
        }

        return
            $"{bytesPerSecond.Value / 1024d:F1} KB/s";
    }

    private static string FormatBytes(
        long bytes)
    {
        if (bytes >= 1024L * 1024L * 1024L)
        {
            return
                $"{bytes /
                   (1024d * 1024d * 1024d):F2} GB";
        }

        return
            $"{bytes /
               (1024d * 1024d):F1} MB";
    }
}