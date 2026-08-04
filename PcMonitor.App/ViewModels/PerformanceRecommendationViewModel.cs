using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class PerformanceRecommendationViewModel
{
    public PerformanceRecommendationViewModel(
        PerformanceRecommendation item)
    {
        Item = item;
    }

    public PerformanceRecommendation Item { get; }

    public string PriorityText =>
        $"P{Item.Priority}";

    public string CategoryText =>
        Item.Category;

    public string TitleText =>
        Item.Title;

    public string DescriptionText =>
        Item.Description;

    public string ExpectedEffectText =>
        Item.ExpectedEffect;
}
