namespace PcMonitor.App.Services;

public interface IFileSaveDialogService
{
    string? ShowSaveDialog(
        string title,
        string suggestedFileName,
        string filter,
        string defaultExtension);
}