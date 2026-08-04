namespace PcMonitor.Infrastructure.Storage;

public sealed class MonitoringDatabaseOptions
{
    public MonitoringDatabaseOptions(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException(
                "Der Datenbankpfad darf nicht leer sein.",
                nameof(databasePath));
        }

        DatabasePath = Path.GetFullPath(databasePath);
    }

    public string DatabasePath { get; }
}