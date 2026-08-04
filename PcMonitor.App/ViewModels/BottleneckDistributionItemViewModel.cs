using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class BottleneckDistributionItemViewModel
{
    public BottleneckDistributionItemViewModel(
        BottleneckDistributionItem item)
    {
        Item =
            item
            ?? throw new ArgumentNullException(
                nameof(item));
    }

    public BottleneckDistributionItem Item { get; }

    public string TypeText =>
        Item.Type switch
        {
            BottleneckType.Gpu =>
                "GPU-Limit",

            BottleneckType.CpuMainThread =>
                "CPU-/Main-Thread",

            BottleneckType.Memory =>
                "Arbeitsspeicher",

            BottleneckType.Vram =>
                "Grafikspeicher",

            BottleneckType.DiskStreaming =>
                "Datenträger/Nachladen",

            BottleneckType.Network =>
                "Netzwerk",

            BottleneckType.BackgroundProcess =>
                "Hintergrundprozess",

            BottleneckType.Mixed =>
                "Gemischt",

            _ =>
                "Unklar"
        };

    public string ShareText =>
        $"{Item.SharePercent:F1} %";

    public string EpisodeCountText =>
        Item.EpisodeCount.ToString("N0");

    public string DurationText =>
        $"{Item.WeightedDurationSeconds:F2} s";
}
