using Microsoft.Win32;

namespace PcMonitor.App.Services;

public sealed class FileSaveDialogService :
    IFileSaveDialogService
{
    public string? ShowSaveDialog(
        string title,
        string suggestedFileName,
        string filter,
        string defaultExtension)
    {
        var dialog =
            new SaveFileDialog
            {
                Title = title,
                FileName = suggestedFileName,
                Filter = filter,
                DefaultExt = defaultExtension,
                AddExtension = true,
                OverwritePrompt = true
            };

        bool? result =
            dialog.ShowDialog();

        return result == true
            ? dialog.FileName
            : null;
    }
}