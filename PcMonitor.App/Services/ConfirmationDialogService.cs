using System.Windows;

namespace PcMonitor.App.Services;

public sealed class ConfirmationDialogService :
    IConfirmationDialogService
{
    public bool Confirm(
        string title,
        string message)
    {
        MessageBoxResult result =
            MessageBox.Show(
                message,
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }
}