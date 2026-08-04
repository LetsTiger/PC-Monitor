using PcMonitor.Core.Models;

namespace PcMonitor.Application.Analysis;

public sealed class PerformanceAnalysisService :
    IPerformanceAnalysisService
{
    public BottleneckAnalysis AnalyzeBottleneck(
        Guid runId,
        RunAnalysis? runAnalysis,
        IReadOnlyList<DiskRunAnalysis> diskAnalyses,
        IReadOnlyList<ProcessRunAnalysis> processAnalyses,
        IReadOnlyList<CpuCoreRunAnalysis> cpuCoreAnalyses,
        IReadOnlyList<FrameRunAnalysis> frameAnalyses,
        IReadOnlyList<NetworkIncident> networkIncidents)
    {
        FrameRunAnalysis? game =
            frameAnalyses.FirstOrDefault(item => item.IsLikelyGame)
            ?? frameAnalyses.FirstOrDefault();

        double gpuScore = Clamp(
            (runAnalysis?.AverageGpuLoadPercent ?? 0) >= 95 ? 92 :
            (runAnalysis?.AverageGpuLoadPercent ?? 0) >= 90 ? 78 :
            (runAnalysis?.MaximumGpuLoadPercent ?? 0) >= 98 ? 55 : 15);

        double hottestThreadShare =
            cpuCoreAnalyses.Count == 0
                ? 0
                : cpuCoreAnalyses.Max(item => item.ShareAbove95Percent);

        double cpuScore = Clamp(
            hottestThreadShare >= 30 ? 94 :
            hottestThreadShare >= 15 ? 80 :
            hottestThreadShare >= 5 ? 58 : 15);

        if ((runAnalysis?.AverageGpuLoadPercent ?? 100) < 85 &&
            hottestThreadShare >= 10)
        {
            cpuScore += 10;
        }

        double memoryScore = Clamp(
            (runAnalysis?.MaximumMemoryUsagePercent ?? 0) >= 95 ? 92 :
            (runAnalysis?.MaximumMemoryUsagePercent ?? 0) >= 90 ? 72 : 10);

        double vramShare =
            runAnalysis?.MaximumGpuUsedMemoryMegabytes is not null &&
            runAnalysis.GpuTotalMemoryMegabytes is > 0
                ? runAnalysis.MaximumGpuUsedMemoryMegabytes.Value /
                  runAnalysis.GpuTotalMemoryMegabytes.Value * 100
                : 0;

        double vramScore = Clamp(
            vramShare >= 97 ? 94 :
            vramShare >= 92 ? 78 :
            vramShare >= 85 ? 52 : 10);

        double maximumDiskActivity =
            diskAnalyses
                .Select(item => item.MaximumTotalActivityPercent ?? 0)
                .DefaultIfEmpty(0)
                .Max();

        double diskScore = Clamp(
            maximumDiskActivity >= 99 ? 86 :
            maximumDiskActivity >= 95 ? 70 :
            maximumDiskActivity >= 85 ? 45 : 10);

        double networkScore = Clamp(
            networkIncidents.Count > 0 ? 82 :
            (runAnalysis?.MaximumInternetPacketLossPercent ?? 0) >= 5 ? 68 :
            (runAnalysis?.AverageInternetJitterMilliseconds ?? 0) >= 20 ? 55 : 5);

        ProcessRunAnalysis? backgroundProcess =
            processAnalyses
                .Where(item => item.ForegroundSharePercent < 20)
                .OrderByDescending(item => item.AverageCpuUsagePercent)
                .FirstOrDefault();

        double backgroundScore = Clamp(
            backgroundProcess?.AverageCpuUsagePercent >= 25 ? 78 :
            backgroundProcess?.MaximumCpuUsagePercent >= 40 ? 58 : 8);

        if (game is not null &&
            game.FrameSpikesAbove50Milliseconds > Math.Max(3, game.FrameCount / 500))
        {
            gpuScore += (runAnalysis?.AverageGpuLoadPercent ?? 0) >= 90 ? 8 : 0;
            cpuScore += hottestThreadShare >= 10 ? 8 : 0;
            diskScore += maximumDiskActivity >= 90 ? 8 : 0;
        }

        var scores = new Dictionary<BottleneckType, double>
        {
            [BottleneckType.Gpu] = Clamp(gpuScore),
            [BottleneckType.CpuMainThread] = Clamp(cpuScore),
            [BottleneckType.Memory] = Clamp(memoryScore),
            [BottleneckType.Vram] = Clamp(vramScore),
            [BottleneckType.DiskStreaming] = Clamp(diskScore),
            [BottleneckType.Network] = Clamp(networkScore),
            [BottleneckType.BackgroundProcess] = Clamp(backgroundScore)
        };

        KeyValuePair<BottleneckType, double>[] ordered =
            scores.OrderByDescending(item => item.Value).ToArray();

        BottleneckType primary =
            ordered[0].Value < 45
                ? BottleneckType.Unknown
                : ordered.Length > 1 &&
                  ordered[0].Value - ordered[1].Value < 8 &&
                  ordered[1].Value >= 55
                    ? BottleneckType.Mixed
                    : ordered[0].Key;

        double confidence =
            primary == BottleneckType.Mixed
                ? Math.Min(90, (ordered[0].Value + ordered[1].Value) / 2)
                : ordered[0].Value;

        (string title, string description, string recommendation) =
            CreateText(primary, confidence, backgroundProcess);

        return new BottleneckAnalysis(
            runId,
            primary,
            confidence,
            title,
            description,
            recommendation,
            Clamp(gpuScore),
            Clamp(cpuScore),
            Clamp(memoryScore),
            Clamp(vramScore),
            Clamp(diskScore),
            Clamp(networkScore),
            Clamp(backgroundScore));
    }


    public StutterAnalysisResult AnalyzeStutters(
        Guid runId,
        IReadOnlyList<FrameChartPoint> framePoints)
    {
        ArgumentNullException.ThrowIfNull(framePoints);

        FrameChartPoint[] ordered =
            framePoints
                .OrderBy(point => point.Timestamp)
                .ToArray();

        if (ordered.Length == 0)
        {
            return new StutterAnalysisResult(
                runId,
                [],
                [],
                0,
                0);
        }

        var episodeGroups =
            new List<List<FrameChartPoint>>();

        List<FrameChartPoint>? current = null;

        foreach (FrameChartPoint point in ordered)
        {
            bool relevant =
                point.FrameTimeMilliseconds >= 25;

            if (!relevant)
            {
                continue;
            }

            if (current is null)
            {
                current = [point];
                episodeGroups.Add(current);
                continue;
            }

            FrameChartPoint previous =
                current[^1];

            TimeSpan gap =
                point.Timestamp -
                previous.Timestamp;

            if (gap <= TimeSpan.FromSeconds(1.25))
            {
                current.Add(point);
            }
            else
            {
                current = [point];
                episodeGroups.Add(current);
            }
        }

        var episodes =
            new List<StutterEpisode>();

        foreach (List<FrameChartPoint> group
                 in episodeGroups)
        {
            if (group.Count == 0)
            {
                continue;
            }

            BottleneckType cause =
                DetermineEpisodeCause(
                    group,
                    out double confidence,
                    out string title,
                    out string description);

            episodes.Add(
                new StutterEpisode(
                    RunId: runId,
                    StartedAt: group[0].Timestamp,
                    EndedAt: group[^1].Timestamp,
                    FrameCount: group.Count,
                    SpikeCount:
                        group.Count(point =>
                            point.FrameTimeMilliseconds >= 25),
                    AverageFrameTimeMilliseconds:
                        group.Average(point =>
                            point.FrameTimeMilliseconds),
                    MaximumFrameTimeMilliseconds:
                        group.Max(point =>
                            point.FrameTimeMilliseconds),
                    MinimumFramesPerSecond:
                        group.Min(point =>
                            point.FramesPerSecond),
                    Cause: cause,
                    ConfidencePercent: confidence,
                    Title: title,
                    Description: description,
                    AverageCpuUsagePercent:
                        AverageNullable(
                            group.Select(point =>
                                point.CpuUsagePercent)),
                    MaximumCpuThreadLoadPercent:
                        MaximumNullable(
                            group.Select(point =>
                                point.MaximumCpuThreadLoadPercent)),
                    AverageGpuLoadPercent:
                        AverageNullable(
                            group.Select(point =>
                                point.GpuLoadPercent)),
                    MaximumMemoryUsagePercent:
                        MaximumNullable(
                            group.Select(point =>
                                point.MemoryUsagePercent)),
                    MaximumVramUsagePercent:
                        MaximumNullable(
                            group.Select(point =>
                                point.VramUsagePercent)),
                    MaximumDiskActivityPercent:
                        MaximumNullable(
                            group.Select(point =>
                                point.DiskActivityPercent)),
                    MaximumDiskReadMegabytesPerSecond:
                        MaximumNullable(
                            group.Select(point =>
                                point.DiskReadMegabytesPerSecond)),
                    BackgroundProcessName:
                        group
                            .Where(point =>
                                !string.IsNullOrWhiteSpace(
                                    point.BackgroundProcessName))
                            .OrderByDescending(point =>
                                point.BackgroundProcessCpuPercent ?? 0)
                            .Select(point =>
                                point.BackgroundProcessName)
                            .FirstOrDefault(),
                    BackgroundProcessCpuPercent:
                        MaximumNullable(
                            group.Select(point =>
                                point.BackgroundProcessCpuPercent))));
        }

        double totalWeight =
            episodes.Sum(GetEpisodeWeight);

        IReadOnlyList<BottleneckDistributionItem> distribution =
            totalWeight <= 0
                ? []
                : episodes
                    .GroupBy(episode => episode.Cause)
                    .Select(group =>
                    {
                        double weight =
                            group.Sum(GetEpisodeWeight);

                        return new BottleneckDistributionItem(
                            Type: group.Key,
                            EpisodeCount: group.Count(),
                            SharePercent:
                                weight / totalWeight * 100,
                            WeightedDurationSeconds:
                                group.Sum(episode =>
                                    Math.Max(
                                        0.05,
                                        episode.Duration.TotalSeconds)));
                    })
                    .OrderByDescending(item =>
                        item.SharePercent)
                    .ToArray();

        return new StutterAnalysisResult(
            RunId: runId,
            Episodes:
                episodes
                    .OrderByDescending(episode =>
                        episode.MaximumFrameTimeMilliseconds)
                    .ToArray(),
            Distribution: distribution,
            TotalSpikeCount:
                episodes.Sum(episode =>
                    episode.SpikeCount),
            TotalStutterDurationSeconds:
                episodes.Sum(episode =>
                    Math.Max(
                        0.05,
                        episode.Duration.TotalSeconds)));
    }

    private static BottleneckType DetermineEpisodeCause(
        IReadOnlyList<FrameChartPoint> points,
        out double confidence,
        out string title,
        out string description)
    {
        double gpu =
            AverageNullable(
                points.Select(point =>
                    point.GpuLoadPercent)) ?? 0;

        double cpuThread =
            MaximumNullable(
                points.Select(point =>
                    point.MaximumCpuThreadLoadPercent)) ?? 0;

        double memory =
            MaximumNullable(
                points.Select(point =>
                    point.MemoryUsagePercent)) ?? 0;

        double vram =
            MaximumNullable(
                points.Select(point =>
                    point.VramUsagePercent)) ?? 0;

        double disk =
            MaximumNullable(
                points.Select(point =>
                    point.DiskActivityPercent)) ?? 0;

        double diskRead =
            MaximumNullable(
                points.Select(point =>
                    point.DiskReadMegabytesPerSecond)) ?? 0;

        FrameChartPoint? background =
            points
                .Where(point =>
                    !string.IsNullOrWhiteSpace(
                        point.BackgroundProcessName))
                .OrderByDescending(point =>
                    point.BackgroundProcessCpuPercent ?? 0)
                .FirstOrDefault();

        bool networkFailure =
            points.Any(point =>
                point.GatewaySuccess == false ||
                point.InternetSuccess == false);

        var scores =
            new Dictionary<BottleneckType, double>
            {
                [BottleneckType.Network] =
                    networkFailure ? 96 : 0,

                [BottleneckType.DiskStreaming] =
                    disk >= 98 && diskRead >= 25
                        ? 92
                        : disk >= 95
                            ? 78
                            : diskRead >= 150
                                ? 62
                                : 0,

                [BottleneckType.CpuMainThread] =
                    cpuThread >= 98 && gpu < 92
                        ? 94
                        : cpuThread >= 95 && gpu < 90
                            ? 86
                            : cpuThread >= 90 && gpu < 85
                                ? 68
                                : 0,

                [BottleneckType.Gpu] =
                    gpu >= 98
                        ? 92
                        : gpu >= 95
                            ? 84
                            : gpu >= 92
                                ? 68
                                : 0,

                [BottleneckType.Vram] =
                    vram >= 98
                        ? 94
                        : vram >= 95
                            ? 80
                            : vram >= 90
                                ? 62
                                : 0,

                [BottleneckType.Memory] =
                    memory >= 97
                        ? 92
                        : memory >= 93
                            ? 76
                            : memory >= 90
                                ? 60
                                : 0,

                [BottleneckType.BackgroundProcess] =
                    (background?.BackgroundProcessCpuPercent ?? 0) >= 25
                        ? 88
                        : (background?.BackgroundProcessCpuPercent ?? 0) >= 15
                            ? 70
                            : 0
            };

        KeyValuePair<BottleneckType, double>[] ordered =
            scores
                .OrderByDescending(item =>
                    item.Value)
                .ToArray();

        if (ordered[0].Value < 55)
        {
            confidence = 35;
            title =
                "Stutter ohne eindeutige Ursache";

            description =
                $"GPU {gpu:F1} %, stärkster CPU-Thread " +
                $"{cpuThread:F1} %, Datenträger {disk:F1} %.";

            return BottleneckType.Unknown;
        }

        if (ordered.Length > 1 &&
            ordered[1].Value >= 65 &&
            ordered[0].Value -
            ordered[1].Value < 10)
        {
            confidence =
                Math.Min(
                    90,
                    (ordered[0].Value +
                     ordered[1].Value) / 2);

            title =
                "Stutter mit gemischter Ursache";

            description =
                $"Mehrere Ursachen waren gleichzeitig stark: " +
                $"{ordered[0].Key} und {ordered[1].Key}.";

            return BottleneckType.Mixed;
        }

        BottleneckType cause =
            ordered[0].Key;

        confidence =
            ordered[0].Value;

        (title, description) =
            cause switch
            {
                BottleneckType.Gpu => (
                    "Stutter bei GPU-Limit",
                    $"GPU-Auslastung durchschnittlich {gpu:F1} %."),

                BottleneckType.CpuMainThread => (
                    "Stutter bei CPU-/Main-Thread-Limit",
                    $"Stärkster CPU-Thread {cpuThread:F1} %, " +
                    $"GPU {gpu:F1} %."),

                BottleneckType.Memory => (
                    "Stutter bei hohem RAM-Druck",
                    $"RAM-Auslastung bis {memory:F1} %."),

                BottleneckType.Vram => (
                    "Stutter bei hohem VRAM-Druck",
                    $"VRAM-Belegung bis {vram:F1} %."),

                BottleneckType.DiskStreaming => (
                    "Stutter durch möglichen Nachladevorgang",
                    $"Datenträgeraktivität bis {disk:F1} %, " +
                    $"Leserate bis {diskRead:F1} MB/s."),

                BottleneckType.Network => (
                    "Stutter während einer Netzwerkstörung",
                    "Gateway oder Internetziel war während der Episode nicht erreichbar."),

                BottleneckType.BackgroundProcess => (
                    "Stutter durch möglichen Hintergrundprozess",
                    $"{background?.BackgroundProcessName ?? "Ein Hintergrundprozess"} " +
                    $"erreichte {background?.BackgroundProcessCpuPercent ?? 0:F1} % CPU."),

                _ => (
                    "Stutter ohne eindeutige Ursache",
                    "Keine Ursache überschritt den erforderlichen Schwellenwert.")
            };

        return cause;
    }

    private static double GetEpisodeWeight(
        StutterEpisode episode)
    {
        double severity =
            Math.Max(
                1,
                episode.MaximumFrameTimeMilliseconds / 25);

        double duration =
            Math.Max(
                0.05,
                episode.Duration.TotalSeconds);

        return severity *
               Math.Max(1, episode.SpikeCount) *
               Math.Sqrt(duration);
    }

    private static double? AverageNullable(
        IEnumerable<double?> values)
    {
        double[] available =
            values
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToArray();

        return available.Length == 0
            ? null
            : available.Average();
    }

    private static double? MaximumNullable(
        IEnumerable<double?> values)
    {
        double[] available =
            values
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToArray();

        return available.Length == 0
            ? null
            : available.Max();
    }

    public RunComparison CreateComparison(
        Guid baseRunId,
        string baseRunName,
        Guid comparisonRunId,
        string comparisonRunName,
        RunAnalysis? baseAnalysis,
        RunAnalysis? comparisonAnalysis,
        IReadOnlyList<FrameRunAnalysis> baseFrames,
        IReadOnlyList<FrameRunAnalysis> comparisonFrames,
        BottleneckAnalysis baseBottleneck,
        BottleneckAnalysis comparisonBottleneck)
    {
        FrameRunAnalysis? left =
            baseFrames.FirstOrDefault(item => item.IsLikelyGame)
            ?? baseFrames.FirstOrDefault();

        FrameRunAnalysis? right =
            comparisonFrames.FirstOrDefault(item => item.IsLikelyGame)
            ?? comparisonFrames.FirstOrDefault();

        double? fpsDifference =
            Difference(left?.AverageFramesPerSecond, right?.AverageFramesPerSecond);

        double? fpsDifferencePercent =
            left?.AverageFramesPerSecond is > 0 &&
            right is not null
                ? (right.AverageFramesPerSecond - left.AverageFramesPerSecond) /
                  left.AverageFramesPerSecond * 100
                : null;

        return new RunComparison(
            baseRunId,
            baseRunName,
            comparisonRunId,
            comparisonRunName,
            left?.AverageFramesPerSecond,
            right?.AverageFramesPerSecond,
            fpsDifference,
            fpsDifferencePercent,
            left?.OnePercentLowFramesPerSecond,
            right?.OnePercentLowFramesPerSecond,
            Difference(left?.OnePercentLowFramesPerSecond,
                       right?.OnePercentLowFramesPerSecond),
            left?.P99FrameTimeMilliseconds,
            right?.P99FrameTimeMilliseconds,
            Difference(left?.P99FrameTimeMilliseconds,
                       right?.P99FrameTimeMilliseconds),
            left?.FrameSpikesAbove25Milliseconds ?? 0,
            right?.FrameSpikesAbove25Milliseconds ?? 0,
            (right?.FrameSpikesAbove25Milliseconds ?? 0) -
            (left?.FrameSpikesAbove25Milliseconds ?? 0),
            baseAnalysis?.AverageGpuLoadPercent,
            comparisonAnalysis?.AverageGpuLoadPercent,
            baseAnalysis?.MaximumCpuUsagePercent,
            comparisonAnalysis?.MaximumCpuUsagePercent,
            baseBottleneck.PrimaryType,
            comparisonBottleneck.PrimaryType);
    }

    private static double? Difference(double? left, double? right) =>
        left is null || right is null ? null : right.Value - left.Value;

    private static double Clamp(double value) =>
        Math.Clamp(value, 0, 100);

    private static (string, string, string) CreateText(
        BottleneckType type,
        double confidence,
        ProcessRunAnalysis? backgroundProcess)
    {
        string confidenceText = $"{confidence:F0} % Vertrauen";

        return type switch
        {
            BottleneckType.Gpu => (
                "Primäres Bottleneck: GPU-Limit",
                $"Die GPU war im Verhältnis zu CPU und übrigen Komponenten am stärksten ausgelastet ({confidenceText}).",
                "Auflösung, Raytracing, Schatten oder andere GPU-intensive Einstellungen reduzieren."),

            BottleneckType.CpuMainThread => (
                "Primäres Bottleneck: CPU-/Main-Thread-Limit",
                $"Mindestens ein CPU-Thread war häufig am Limit, während die GPU nicht dauerhaft voll ausgelastet war ({confidenceText}).",
                "CPU-intensive Spieloptionen, Hintergrundprozesse und Energieprofil prüfen."),

            BottleneckType.Memory => (
                "Primäres Bottleneck: Arbeitsspeicher",
                $"Der Arbeitsspeicher erreichte einen kritischen Belegungsbereich ({confidenceText}).",
                "Speicherintensive Hintergrundanwendungen schließen und Auslagerung beobachten."),

            BottleneckType.Vram => (
                "Primäres Bottleneck: Grafikspeicher",
                $"Die VRAM-Belegung lag nahe an der verfügbaren Kapazität ({confidenceText}).",
                "Texturqualität, Auflösung oder hochauflösende Texturpakete reduzieren."),

            BottleneckType.DiskStreaming => (
                "Primäres Bottleneck: Datenträger/Nachladen",
                $"Datenträgeraktivität und Frametime-Auffälligkeiten sprechen für Nachladevorgänge ({confidenceText}).",
                "Spiel auf schnellen Datenträger verschieben und parallele I/O-Last prüfen."),

            BottleneckType.Network => (
                "Primäres Bottleneck: Netzwerk",
                $"Abbrüche, Paketverlust oder hoher Jitter waren die stärkste Auffälligkeit ({confidenceText}).",
                "Gateway, WLAN/LAN, Bufferbloat und Provider-Verbindung prüfen."),

            BottleneckType.BackgroundProcess => (
                "Primäres Bottleneck: Hintergrundprozess",
                $"{backgroundProcess?.ProcessName ?? "Ein Hintergrundprozess"} erzeugte relevante Parallelbelastung ({confidenceText}).",
                "Den auffälligen Prozess während des Spielens schließen oder begrenzen."),

            BottleneckType.Mixed => (
                "Gemischtes Bottleneck",
                $"Mindestens zwei Ursachen waren ähnlich stark ausgeprägt ({confidenceText}).",
                "Im erweiterten Modus die Ereignis-Timeline öffnen und einzelne Frametime-Spikes nach Ursache prüfen."),

            _ => (
                "Kein eindeutiges Bottleneck",
                "Die Messwerte ergeben keine ausreichend klare primäre Ursache.",
                "Einen längeren und reproduzierbaren Run durchführen.")
        };
    }

    public GameProfile DetectGameProfile(
        string? gameName)
    {
        string normalized =
            (gameName ?? string.Empty)
                .Replace(".exe", string.Empty,
                    StringComparison.OrdinalIgnoreCase)
                .Trim()
                .ToLowerInvariant();

        if (normalized.Contains("rdr2") ||
            normalized.Contains("red dead"))
        {
            return new GameProfile(
                "rdr2",
                "Red Dead Redemption 2",
                "Open-World-Profil mit höherer Gewichtung für Frametime-Stabilität, Streaming und VRAM.",
                0.20, 0.20, 0.20, 0.20, 0.15, 0.05,
                60, 25);
        }

        if (normalized.Contains("cyberpunk"))
        {
            return new GameProfile(
                "cyberpunk",
                "Cyberpunk 2077",
                "GPU- und VRAM-lastiges Profil mit Fokus auf P99-Frametime und Stabilität.",
                0.18, 0.18, 0.22, 0.20, 0.17, 0.05,
                60, 25);
        }

        if (normalized.Contains("cs2") ||
            normalized.Contains("counter-strike"))
        {
            return new GameProfile(
                "cs2",
                "Counter-Strike 2",
                "Latenz- und CPU-orientiertes Profil mit hohem FPS- und 1-%-Low-Gewicht.",
                0.28, 0.25, 0.16, 0.16, 0.10, 0.05,
                144, 12);
        }

        if (normalized.Contains("flightsimulator") ||
            normalized.Contains("flight simulator") ||
            normalized.Contains("fs2024") ||
            normalized.Contains("fs2020"))
        {
            return new GameProfile(
                "msfs",
                "Microsoft Flight Simulator",
                "Main-Thread- und Streaming-Profil mit großzügigerem FPS-Ziel.",
                0.14, 0.20, 0.22, 0.20, 0.19, 0.05,
                45, 33);
        }

        return new GameProfile(
            "generic",
            string.IsNullOrWhiteSpace(gameName)
                ? "Generisches Gaming-Profil"
                : gameName.Trim(),
            "Ausgewogenes Profil für unbekannte Spiele.",
            0.22, 0.20, 0.20, 0.18, 0.15, 0.05,
            60, 25);
    }

    public PerformanceScoreResult CreatePerformanceScore(
        Guid runId,
        GameProfile profile,
        RunAnalysis? runAnalysis,
        FrameRunAnalysis? frameAnalysis,
        BottleneckAnalysis bottleneck,
        StutterAnalysisResult stutterAnalysis)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(bottleneck);
        ArgumentNullException.ThrowIfNull(stutterAnalysis);

        if (frameAnalysis is null)
        {
            return new PerformanceScoreResult(
                runId, 0, "–", 0, 0, 0, 0, 0, 0,
                "Keine verwertbaren FPS-Daten vorhanden.");
        }

        double fpsScore = ScoreHigherIsBetter(
            frameAnalysis.AverageFramesPerSecond,
            profile.TargetFramesPerSecond * 0.55,
            profile.TargetFramesPerSecond);

        double lowFpsScore = ScoreHigherIsBetter(
            frameAnalysis.OnePercentLowFramesPerSecond,
            profile.TargetFramesPerSecond * 0.35,
            profile.TargetFramesPerSecond * 0.80);

        double frameTimeScore = ScoreLowerIsBetter(
            frameAnalysis.P99FrameTimeMilliseconds,
            profile.AcceptableP99FrameTimeMilliseconds * 2.4,
            profile.AcceptableP99FrameTimeMilliseconds);

        double spikeRate =
            frameAnalysis.FrameCount <= 0
                ? 1
                : (double)frameAnalysis.FrameSpikesAbove25Milliseconds /
                  frameAnalysis.FrameCount;

        double episodePenalty =
            Math.Min(
                35,
                stutterAnalysis.Episodes.Count * 2.5 +
                stutterAnalysis.TotalStutterDurationSeconds * 1.5);

        double stabilityScore =
            Clamp(100 - spikeRate * 3_500 - episodePenalty);

        double thermalPenalty = 0;

        if ((runAnalysis?.MaximumCpuCoreTemperatureCelsius ?? 0) >= 95)
        {
            thermalPenalty += 25;
        }
        else if ((runAnalysis?.MaximumCpuCoreTemperatureCelsius ?? 0) >= 88)
        {
            thermalPenalty += 10;
        }

        if ((runAnalysis?.MaximumGpuTemperatureCelsius ?? 0) >= 88)
        {
            thermalPenalty += 25;
        }
        else if ((runAnalysis?.MaximumGpuTemperatureCelsius ?? 0) >= 82)
        {
            thermalPenalty += 10;
        }

        double bottleneckPenalty =
            Math.Max(
                0,
                bottleneck.ConfidencePercent - 55) * 0.35;

        double hardwareScore =
            Clamp(100 - thermalPenalty - bottleneckPenalty);

        double networkScore =
            Clamp(
                100 -
                (runAnalysis?.InternetFailureCount ?? 0) * 15 -
                (runAnalysis?.MaximumInternetPacketLossPercent ?? 0) * 5 -
                Math.Max(
                    0,
                    (runAnalysis?.AverageInternetJitterMilliseconds ?? 0) - 10) *
                1.5);

        double total =
            fpsScore * profile.FpsWeight +
            lowFpsScore * profile.LowFpsWeight +
            frameTimeScore * profile.FrameTimeWeight +
            stabilityScore * profile.StabilityWeight +
            hardwareScore * profile.HardwareWeight +
            networkScore * profile.NetworkWeight;

        total = Clamp(total);

        string grade =
            total >= 92 ? "A+" :
            total >= 85 ? "A" :
            total >= 75 ? "B" :
            total >= 65 ? "C" :
            total >= 50 ? "D" : "E";

        string summary =
            total >= 85
                ? "Sehr stabile Gaming-Performance."
                : total >= 70
                    ? "Gute Performance mit einzelnen optimierbaren Bereichen."
                    : total >= 55
                        ? "Spürbare Schwächen bei Stabilität oder Auslastung."
                        : "Deutliche Performance-Probleme wurden erkannt.";

        return new PerformanceScoreResult(
            runId,
            total,
            grade,
            fpsScore,
            lowFpsScore,
            frameTimeScore,
            stabilityScore,
            hardwareScore,
            networkScore,
            summary);
    }

    public AdvancedPerformanceAnalysis AnalyzeAdvanced(
        RunSummary currentRun,
        string gameName,
        RunAnalysis? runAnalysis,
        FrameRunAnalysis? frameAnalysis,
        IReadOnlyList<FrameChartPoint> framePoints,
        BottleneckAnalysis bottleneck,
        StutterAnalysisResult stutterAnalysis,
        IReadOnlyList<RunPerformanceSnapshot> historicalRuns)
    {
        ArgumentNullException.ThrowIfNull(currentRun);
        ArgumentNullException.ThrowIfNull(framePoints);
        ArgumentNullException.ThrowIfNull(bottleneck);
        ArgumentNullException.ThrowIfNull(stutterAnalysis);
        ArgumentNullException.ThrowIfNull(historicalRuns);

        GameProfile profile =
            DetectGameProfile(gameName);

        PerformanceScoreResult score =
            CreatePerformanceScore(
                currentRun.Id,
                profile,
                runAnalysis,
                frameAnalysis,
                bottleneck,
                stutterAnalysis);

        RunPerformanceSnapshot currentSnapshot =
            new(
                currentRun,
                gameName,
                runAnalysis,
                frameAnalysis,
                bottleneck,
                stutterAnalysis,
                score);

        RunPerformanceSnapshot[] comparable =
            historicalRuns
                .Where(item =>
                    string.Equals(
                        NormalizeGameName(item.GameName),
                        NormalizeGameName(gameName),
                        StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.Run.StartedAt)
                .ToArray();

        return new AdvancedPerformanceAnalysis(
            profile,
            score,
            CreateRecommendations(
                profile,
                runAnalysis,
                frameAnalysis,
                bottleneck,
                stutterAnalysis),
            CreateTimelineStories(stutterAnalysis),
            CreateHeatmap(framePoints),
            CreateRegression(currentSnapshot, comparable),
            CreateSessionStatistics(currentSnapshot, comparable),
            CreateTrends(currentSnapshot, comparable));
    }

    private static IReadOnlyList<PerformanceRecommendation>
        CreateRecommendations(
            GameProfile profile,
            RunAnalysis? analysis,
            FrameRunAnalysis? frame,
            BottleneckAnalysis bottleneck,
            StutterAnalysisResult stutters)
    {
        var result =
            new List<PerformanceRecommendation>();

        switch (bottleneck.PrimaryType)
        {
            case BottleneckType.Gpu:
                result.Add(new(
                    1, "GPU",
                    "GPU-intensive Einstellungen reduzieren",
                    "Auflösung, Raytracing, Schatten und volumetrische Effekte schrittweise senken.",
                    "Höhere FPS und stabilere Frametime."));
                break;

            case BottleneckType.CpuMainThread:
                result.Add(new(
                    1, "CPU",
                    "CPU-/Main-Thread-Last reduzieren",
                    "Sichtweite, NPC-Dichte, Physik und Hintergrundprogramme prüfen.",
                    "Bessere 1-%-Lows und weniger CPU-Stutter."));
                break;

            case BottleneckType.Vram:
                result.Add(new(
                    1, "VRAM",
                    "VRAM-Belegung senken",
                    "Texturqualität, Texturpakete und Auflösung reduzieren.",
                    "Weniger Nachladeruckler und Speicherüberläufe."));
                break;

            case BottleneckType.Memory:
                result.Add(new(
                    1, "RAM",
                    "Arbeitsspeicher freigeben",
                    "Speicherintensive Programme schließen und Auslagerungsdatei prüfen.",
                    "Weniger Paging und gleichmäßigere Frametime."));
                break;

            case BottleneckType.DiskStreaming:
                result.Add(new(
                    1, "Datenträger",
                    "Streaming-Last reduzieren",
                    "Spiel auf SSD/NVMe betreiben und parallele Downloads oder Scanner pausieren.",
                    "Weniger Stutter beim Nachladen."));
                break;

            case BottleneckType.Network:
                result.Add(new(
                    1, "Netzwerk",
                    "Netzwerkstabilität verbessern",
                    "LAN bevorzugen, Router-Auslastung und Bufferbloat prüfen.",
                    "Weniger Paketverlust und Latenzspitzen."));
                break;

            case BottleneckType.BackgroundProcess:
                result.Add(new(
                    1, "Prozesse",
                    "Auffällige Hintergrundprozesse schließen",
                    "Die Prozessanalyse nach CPU- und Datenträgerlast sortieren.",
                    "Mehr freie CPU-Zeit und stabilere Frametimes."));
                break;
        }

        if (stutters.Episodes.Count >= 5)
        {
            result.Add(new(
                2, "Stabilität",
                "Stutter-Episoden gezielt reproduzieren",
                "Die gleiche Spielsituation erneut testen; Details lassen sich anschließend im erweiterten Modus in der Ereignis-Timeline prüfen.",
                "Ursache lässt sich zuverlässiger bestätigen."));
        }

        if ((analysis?.MaximumCpuCoreTemperatureCelsius ?? 0) >= 90)
        {
            result.Add(new(
                2, "Temperatur",
                "CPU-Kühlung prüfen",
                "Lüfterkurve, Kühlerkontakt und Gehäuse-Airflow kontrollieren.",
                "Weniger thermisches Throttling."));
        }

        if ((analysis?.MaximumGpuTemperatureCelsius ?? 0) >= 84)
        {
            result.Add(new(
                2, "Temperatur",
                "GPU-Temperatur senken",
                "Airflow, Lüfterkurve oder leichtes Undervolting prüfen.",
                "Stabilerer GPU-Takt."));
        }

        if (profile.Id == "rdr2")
        {
            result.Add(new(
                3, "Spielprofil",
                "RDR2-Streaming berücksichtigen",
                "Texturen und Geometrie nicht gleichzeitig stark erhöhen; Vulkan und DX12 getrennt vergleichen.",
                "Bessere P99-Frametime und weniger Streaming-Stutter."));
        }
        else if (profile.Id == "cyberpunk")
        {
            result.Add(new(
                3, "Spielprofil",
                "Upscaling und Raytracing ausbalancieren",
                "DLSS/FSR/XeSS und Raytracing-Stufe in reproduzierbaren Runs vergleichen.",
                "Besseres Verhältnis aus Bildqualität und Stabilität."));
        }
        else if (profile.Id == "cs2")
        {
            result.Add(new(
                3, "Spielprofil",
                "Latenz statt Maximalqualität priorisieren",
                "FPS-Limit, Reflex und CPU-intensive Optionen reproduzierbar testen.",
                "Bessere 1-%-Lows und geringere Eingabelatenz."));
        }
        else if (profile.Id == "msfs")
        {
            result.Add(new(
                3, "Spielprofil",
                "Main-Thread und Terrain-Streaming abstimmen",
                "Terrain LOD, Object LOD und Datenstreaming getrennt testen.",
                "Weniger Main-Thread- und Streaming-Stutter."));
        }

        if (result.Count == 0)
        {
            result.Add(new(
                3, "Allgemein",
                "Aktuelle Konfiguration beibehalten",
                "Es wurde kein klarer kritischer Optimierungsbedarf erkannt.",
                "Stabile Ausgangsbasis für weitere Vergleiche."));
        }

        return result
            .OrderBy(item => item.Priority)
            .ThenBy(item => item.Category)
            .Take(8)
            .ToArray();
    }

    private static IReadOnlyList<TimelineStory>
        CreateTimelineStories(
            StutterAnalysisResult stutters)
    {
        return stutters.Episodes
            .OrderBy(item => item.StartedAt)
            .Take(100)
            .Select(item =>
            {
                string cause =
                    item.Description;

                string impact =
                    $"Frametime stieg auf maximal " +
                    $"{item.MaximumFrameTimeMilliseconds:F1} ms; " +
                    $"Minimum {item.MinimumFramesPerSecond:F1} FPS.";

                string recovery =
                    item.Duration.TotalSeconds <= 0.1
                        ? "Die Darstellung stabilisierte sich unmittelbar nach dem Einzelspike."
                        : $"Die Episode endete nach {Math.Max(0.05, item.Duration.TotalSeconds):F2} s.";

                return new TimelineStory(
                    item.StartedAt,
                    item.EndedAt,
                    item.Title,
                    cause,
                    impact,
                    recovery,
                    item.ConfidencePercent);
            })
            .ToArray();
    }

    private static IReadOnlyList<PerformanceHeatmapCell>
        CreateHeatmap(
            IReadOnlyList<FrameChartPoint> points)
    {
        if (points.Count == 0)
        {
            return [];
        }

        const int segmentCount = 20;

        double maximumSeconds =
            Math.Max(
                1,
                points.Max(item => item.SecondsFromStart));

        double segmentWidth =
            maximumSeconds / segmentCount;

        var cells =
            new List<PerformanceHeatmapCell>();

        for (int segment = 0;
             segment < segmentCount;
             segment++)
        {
            double start =
                segment * segmentWidth;

            double end =
                segment == segmentCount - 1
                    ? maximumSeconds + 0.001
                    : (segment + 1) * segmentWidth;

            FrameChartPoint[] group =
                points
                    .Where(item =>
                        item.SecondsFromStart >= start &&
                        item.SecondsFromStart < end)
                    .ToArray();

            if (group.Length == 0)
            {
                continue;
            }

            AddHeatmapCell(
                cells, segment, start, end,
                "FPS",
                group.Average(item => item.FramesPerSecond),
                1 - Clamp01(
                    group.Average(item => item.FramesPerSecond) / 120),
                $"{group.Average(item => item.FramesPerSecond):F0}");

            AddHeatmapCell(
                cells, segment, start, end,
                "Frametime",
                group.Average(item => item.FrameTimeMilliseconds),
                Clamp01(
                    group.Average(item => item.FrameTimeMilliseconds) / 50),
                $"{group.Average(item => item.FrameTimeMilliseconds):F1} ms");

            AddHeatmapCell(
                cells, segment, start, end,
                "CPU-Thread",
                AverageNullable(group.Select(item =>
                    item.MaximumCpuThreadLoadPercent)) ?? 0,
                (AverageNullable(group.Select(item =>
                    item.MaximumCpuThreadLoadPercent)) ?? 0) / 100,
                $"{AverageNullable(group.Select(item => item.MaximumCpuThreadLoadPercent)) ?? 0:F0} %");

            AddHeatmapCell(
                cells, segment, start, end,
                "GPU",
                AverageNullable(group.Select(item =>
                    item.GpuLoadPercent)) ?? 0,
                (AverageNullable(group.Select(item =>
                    item.GpuLoadPercent)) ?? 0) / 100,
                $"{AverageNullable(group.Select(item => item.GpuLoadPercent)) ?? 0:F0} %");

            AddHeatmapCell(
                cells, segment, start, end,
                "RAM",
                AverageNullable(group.Select(item =>
                    item.MemoryUsagePercent)) ?? 0,
                (AverageNullable(group.Select(item =>
                    item.MemoryUsagePercent)) ?? 0) / 100,
                $"{AverageNullable(group.Select(item => item.MemoryUsagePercent)) ?? 0:F0} %");

            AddHeatmapCell(
                cells, segment, start, end,
                "Disk",
                AverageNullable(group.Select(item =>
                    item.DiskActivityPercent)) ?? 0,
                (AverageNullable(group.Select(item =>
                    item.DiskActivityPercent)) ?? 0) / 100,
                $"{AverageNullable(group.Select(item => item.DiskActivityPercent)) ?? 0:F0} %");
        }

        return cells;
    }

    private static void AddHeatmapCell(
        ICollection<PerformanceHeatmapCell> target,
        int segment,
        double start,
        double end,
        string metric,
        double value,
        double intensity,
        string displayValue)
    {
        target.Add(new PerformanceHeatmapCell(
            segment,
            start,
            end,
            metric,
            value,
            Clamp01(intensity),
            displayValue));
    }

    private static PerformanceRegression CreateRegression(
        RunPerformanceSnapshot current,
        IReadOnlyList<RunPerformanceSnapshot> history)
    {
        if (history.Count == 0)
        {
            return new PerformanceRegression(
                false, 0,
                current.Score.TotalScore, null, null,
                current.FrameAnalysis?.AverageFramesPerSecond,
                null, null,
                current.FrameAnalysis?.OnePercentLowFramesPerSecond,
                null, null,
                "Keine Vergleichsbasis",
                "Für diese Hauptanwendung liegt noch kein früherer vergleichbarer Run vor.");
        }

        double baselineScore =
            history.Average(item =>
                item.Score.TotalScore);

        double? baselineFps =
            AverageNullable(
                history.Select(item =>
                    item.FrameAnalysis?.AverageFramesPerSecond));

        double? baselineLow =
            AverageNullable(
                history.Select(item =>
                    item.FrameAnalysis?.OnePercentLowFramesPerSecond));

        double scoreDifference =
            current.Score.TotalScore -
            baselineScore;

        double? fpsDifferencePercent =
            PercentDifference(
                baselineFps,
                current.FrameAnalysis?.AverageFramesPerSecond);

        double? lowDifferencePercent =
            PercentDifference(
                baselineLow,
                current.FrameAnalysis?.OnePercentLowFramesPerSecond);

        string status =
            scoreDifference <= -7 ||
            fpsDifferencePercent <= -8 ||
            lowDifferencePercent <= -10
                ? "Regression erkannt"
                : scoreDifference >= 7 ||
                  fpsDifferencePercent >= 8
                    ? "Verbesserung erkannt"
                    : "Im normalen Bereich";

        string explanation =
            $"Vergleich mit {history.Count} früheren Run(s): " +
            $"Score {scoreDifference:+0.0;-0.0;0.0}, " +
            $"FPS {(fpsDifferencePercent is null ? "–" : $"{fpsDifferencePercent:+0.0;-0.0;0.0} %")}, " +
            $"1-%-Low {(lowDifferencePercent is null ? "–" : $"{lowDifferencePercent:+0.0;-0.0;0.0} %")}.";

        return new PerformanceRegression(
            true,
            history.Count,
            current.Score.TotalScore,
            baselineScore,
            scoreDifference,
            current.FrameAnalysis?.AverageFramesPerSecond,
            baselineFps,
            fpsDifferencePercent,
            current.FrameAnalysis?.OnePercentLowFramesPerSecond,
            baselineLow,
            lowDifferencePercent,
            status,
            explanation);
    }

    private static GameSessionStatistics CreateSessionStatistics(
        RunPerformanceSnapshot current,
        IReadOnlyList<RunPerformanceSnapshot> history)
    {
        RunPerformanceSnapshot[] all =
            history
                .Append(current)
                .OrderBy(item => item.Run.StartedAt)
                .ToArray();

        return new GameSessionStatistics(
            current.GameName,
            all.Length,
            TimeSpan.FromTicks(
                all.Sum(item =>
                    item.Run.Duration.Ticks)),
            all.Average(item =>
                item.Score.TotalScore),
            all.Max(item =>
                item.Score.TotalScore),
            AverageNullable(all.Select(item =>
                item.FrameAnalysis?.AverageFramesPerSecond)),
            MaximumNullable(all.Select(item =>
                item.FrameAnalysis?.AverageFramesPerSecond)),
            AverageNullable(all.Select(item =>
                item.FrameAnalysis?.OnePercentLowFramesPerSecond)),
            AverageNullable(all.Select(item =>
                item.FrameAnalysis?.P99FrameTimeMilliseconds)),
            all.Average(item =>
                item.StutterAnalysis.Episodes.Count),
            all
                .GroupBy(item =>
                    item.Bottleneck.PrimaryType)
                .OrderByDescending(group =>
                    group.Count())
                .First()
                .Key);
    }

    private static IReadOnlyList<PerformanceTrendPoint>
        CreateTrends(
            RunPerformanceSnapshot current,
            IReadOnlyList<RunPerformanceSnapshot> history)
    {
        return history
            .Append(current)
            .OrderBy(item =>
                item.Run.StartedAt)
            .Select(item =>
                new PerformanceTrendPoint(
                    item.Run.Id,
                    item.Run.Name,
                    item.Run.StartedAt,
                    item.Score.TotalScore,
                    item.FrameAnalysis?.AverageFramesPerSecond,
                    item.FrameAnalysis?.OnePercentLowFramesPerSecond,
                    item.FrameAnalysis?.P99FrameTimeMilliseconds,
                    item.Analysis?.MaximumGpuTemperatureCelsius,
                    item.StutterAnalysis.Episodes.Count,
                    item.Bottleneck.PrimaryType))
            .ToArray();
    }

    private static string NormalizeGameName(
        string value) =>
        value
            .Replace(".exe", string.Empty,
                StringComparison.OrdinalIgnoreCase)
            .Trim()
            .ToLowerInvariant();

    private static double ScoreHigherIsBetter(
        double value,
        double minimum,
        double target)
    {
        if (target <= minimum)
        {
            return 100;
        }

        return Clamp(
            (value - minimum) /
            (target - minimum) * 100);
    }

    private static double ScoreLowerIsBetter(
        double value,
        double poor,
        double good)
    {
        if (poor <= good)
        {
            return 100;
        }

        return Clamp(
            (poor - value) /
            (poor - good) * 100);
    }

    private static double Clamp01(
        double value) =>
        Math.Clamp(value, 0, 1);

    private static double? PercentDifference(
        double? baseline,
        double? current)
    {
        if (baseline is null ||
            current is null ||
            Math.Abs(baseline.Value) < 0.0001)
        {
            return null;
        }

        return
            (current.Value - baseline.Value) /
            baseline.Value * 100;
    }

}
