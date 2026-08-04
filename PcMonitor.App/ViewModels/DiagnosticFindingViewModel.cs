using PcMonitor.Core.Diagnostics;

using System.Windows.Media;

namespace PcMonitor.App.ViewModels;

public sealed class DiagnosticFindingViewModel
{
    public DiagnosticFindingViewModel(
        DiagnosticFinding finding)
    {
        Finding = finding
                  ?? throw new ArgumentNullException(
                      nameof(finding));
    }

    public DiagnosticFinding Finding { get; }

    public string SeverityText =>
        Finding.Severity switch
        {
            DiagnosticSeverity.Critical =>
                "Kritisch",

            DiagnosticSeverity.Warning =>
                "Warnung",

            DiagnosticSeverity.Information =>
                "Hinweis",

            _ => Finding.Severity.ToString()
        };

    public Brush SeverityBrush =>
        Finding.Severity switch
        {
            DiagnosticSeverity.Critical =>
                new SolidColorBrush(Color.FromRgb(214, 69, 69)),
            DiagnosticSeverity.Warning =>
                new SolidColorBrush(Color.FromRgb(224, 157, 45)),
            _ =>
                new SolidColorBrush(Color.FromRgb(78, 155, 214))
        };

    public string CategoryText =>
        Finding.Category switch
        {
            DiagnosticCategory.System =>
                "System",

            DiagnosticCategory.Cpu =>
                "CPU",

            DiagnosticCategory.Gpu =>
                "GPU",

            DiagnosticCategory.Memory =>
                "RAM",

            DiagnosticCategory.Network =>
                "Netzwerk",

            DiagnosticCategory.Disk =>
                "Datenträger",

            DiagnosticCategory.Process =>
                "Prozess",

            _ => Finding.Category.ToString()
        };

    public string TitleText =>
        Finding.Title;

    public string DescriptionText =>
        Finding.Description;

    public string RecommendationText =>
        Finding.Recommendation ?? "–";
}
