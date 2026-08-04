using System.Windows.Media;
using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class PerformanceHeatmapCellViewModel
{
    public PerformanceHeatmapCellViewModel(
        PerformanceHeatmapCell cell)
    {
        Cell = cell;

        byte alpha =
            (byte)(40 + cell.Intensity * 180);

        Background =
            cell.Metric is "FPS"
                ? new SolidColorBrush(
                    Color.FromArgb(
                        alpha, 210, 70, 70))
                : new SolidColorBrush(
                    Color.FromArgb(
                        alpha, 230, 120, 35));
    }

    public PerformanceHeatmapCell Cell { get; }

    public Brush Background { get; }

    public string MetricText => Cell.Metric;

    public string TimeText =>
        $"{Cell.StartedAtSeconds:F0}–{Cell.EndedAtSeconds:F0}s";

    public string ValueText =>
        Cell.DisplayValue;
}
