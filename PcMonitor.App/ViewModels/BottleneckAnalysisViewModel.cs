using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class BottleneckAnalysisViewModel
{
    public BottleneckAnalysisViewModel(BottleneckAnalysis analysis)
    {
        Analysis = analysis;
    }

    public BottleneckAnalysis Analysis { get; }
    public string TitleText => Analysis.Title;
    public string ConfidenceText => $"{Analysis.ConfidencePercent:F0} %";
    public string DescriptionText => Analysis.Description;
    public string RecommendationText => Analysis.Recommendation;
    public string ScoresText =>
        $"GPU {Analysis.GpuScore:F0} · CPU {Analysis.CpuScore:F0} · " +
        $"RAM {Analysis.MemoryScore:F0} · VRAM {Analysis.VramScore:F0} · " +
        $"Disk {Analysis.DiskScore:F0} · Netz {Analysis.NetworkScore:F0}";
}
