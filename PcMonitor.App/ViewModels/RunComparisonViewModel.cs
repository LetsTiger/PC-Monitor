using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class RunComparisonViewModel
{
    public RunComparisonViewModel(RunComparison comparison)
    {
        Comparison = comparison;
    }

    public RunComparison Comparison { get; }
    public string TitleText => $"{Comparison.BaseRunName} ↔ {Comparison.ComparisonRunName}";
    public string AverageFpsText => FormatMetric(
        Comparison.BaseAverageFps,
        Comparison.ComparisonAverageFps,
        Comparison.AverageFpsDifference,
        "FPS");
    public string AverageFpsDifferenceText =>
        Comparison.AverageFpsDifferencePercent is null
            ? "–"
            : $"{Comparison.AverageFpsDifferencePercent:+0.0;-0.0;0.0} %";
    public string OnePercentLowText => FormatMetric(
        Comparison.BaseOnePercentLowFps,
        Comparison.ComparisonOnePercentLowFps,
        Comparison.OnePercentLowDifference,
        "FPS");
    public string P99Text => FormatMetric(
        Comparison.BaseP99FrameTimeMilliseconds,
        Comparison.ComparisonP99FrameTimeMilliseconds,
        Comparison.P99FrameTimeDifferenceMilliseconds,
        "ms");
    public string SpikeText =>
        $"{Comparison.BaseSpikeCount:N0} → {Comparison.ComparisonSpikeCount:N0} " +
        $"({Comparison.SpikeCountDifference:+#;-#;0})";
    public string BottleneckText =>
        $"{FormatBottleneck(Comparison.BaseBottleneck)} → " +
        $"{FormatBottleneck(Comparison.ComparisonBottleneck)}";

    private static string FormatMetric(
        double? left, double? right, double? difference, string unit) =>
        left is null || right is null || difference is null
            ? "–"
            : $"{left:F1} → {right:F1} {unit} " +
              $"({difference:+0.1;-0.1;0.0})";

    private static string FormatBottleneck(BottleneckType value) =>
        value switch
        {
            BottleneckType.Gpu => "GPU",
            BottleneckType.CpuMainThread => "CPU/Main-Thread",
            BottleneckType.Memory => "RAM",
            BottleneckType.Vram => "VRAM",
            BottleneckType.DiskStreaming => "Datenträger",
            BottleneckType.Network => "Netzwerk",
            BottleneckType.BackgroundProcess => "Hintergrundprozess",
            BottleneckType.Mixed => "Gemischt",
            _ => "Unklar"
        };
}
