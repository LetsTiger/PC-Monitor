namespace PcMonitor.Core.Models;

public sealed record RunPerformanceSnapshot(
    RunSummary Run,
    string GameName,
    RunAnalysis? Analysis,
    FrameRunAnalysis? FrameAnalysis,
    BottleneckAnalysis Bottleneck,
    StutterAnalysisResult StutterAnalysis,
    PerformanceScoreResult Score);
