using System.Windows;

namespace PcMonitor.App;

public partial class PresentMonPermissionDialog : Window
{
    public PresentMonPermissionDialog()
    {
        InitializeComponent();
    }

    public bool StartWithPresentMon { get; private set; }
    public bool RememberChoice => RememberChoiceCheckBox.IsChecked == true;

    private void OnWithPresentMonClick(object sender, RoutedEventArgs eventArgs)
    {
        StartWithPresentMon = true;
        DialogResult = true;
    }

    private void OnWithoutPresentMonClick(object sender, RoutedEventArgs eventArgs)
    {
        StartWithPresentMon = false;
        DialogResult = false;
    }
}
