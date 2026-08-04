using PcMonitor.Core.Models;

namespace PcMonitor.Application.Analysis;

public interface IPerformanceAnalysisService
{
    BottleneckAnalysis AnalyzeBottleneck(
        Guid runId,
        RunAnalysis? runAnalysis,
        IReadOnlyList<DiskRunAnalysis> diskAnalyses,
        IReadOnlyList<ProcessRunAnalysis> processAnalyses,
        IReadOnlyList<CpuCoreRunAnalysis> cpuCoreAnalyses,
        IReadOnlyList<FrameRunAnalysis> frameAnalyses,
        IReadOnlyList<NetworkIncident> networkIncidents);


    StutterAnalysisResult AnalyzeStutters(
        Guid runId,
        IReadOnlyList<FrameChartPoint> framePoints);

    GameProfile DetectGameProfile(
        string? gameName);

    PerformanceScoreResult CreatePerformanceScore(
        Guid runId,
        GameProfile profile,
        RunAnalysis? runAnalysis,
        FrameRunAnalysis? frameAnalysis,
        BottleneckAnalysis bottleneck,
        StutterAnalysisResult stutterAnalysis);

    AdvancedPerformanceAnalysis AnalyzeAdvanced(
        RunSummary currentRun,
        string gameName,
        RunAnalysis? runAnalysis,
        FrameRunAnalysis? frameAnalysis,
        IReadOnlyList<FrameChartPoint> framePoints,
        BottleneckAnalysis bottleneck,
        StutterAnalysisResult stutterAnalysis,
        IReadOnlyList<RunPerformanceSnapshot> historicalRuns);

    RunComparison CreateComparison(
        Guid baseRunId,
        string baseRunName,
        Guid comparisonRunId,
        string comparisonRunName,
        RunAnalysis? baseAnalysis,
        RunAnalysis? comparisonAnalysis,
        IReadOnlyList<FrameRunAnalysis> baseFrames,
        IReadOnlyList<FrameRunAnalysis> comparisonFrames,
        BottleneckAnalysis baseBottleneck,
        BottleneckAnalysis comparisonBottleneck);
}
