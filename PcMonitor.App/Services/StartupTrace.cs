using System.IO;
using System.Text;

namespace PcMonitor.App.Services;

internal static class StartupTrace
{
    private const string DebugArgument = "--startup-debug";
    private static readonly object SyncRoot = new();
    private static string? _logFilePath;

    public static bool IsEnabled => _logFilePath is not null;

    public static void Enable(IReadOnlyList<string> arguments)
    {
        if (!arguments.Contains(DebugArgument, StringComparer.OrdinalIgnoreCase)) return;

        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PcMonitor",
            "Logs",
            "Startup");
        Directory.CreateDirectory(directory);
        _logFilePath = Path.Combine(
            directory,
            $"startup-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log");

        Write($"Startup-Debug aktiviert. Argumente: {string.Join(" | ", arguments)}");
    }

    public static void Write(string message)
    {
        string? logFilePath = _logFilePath;
        if (logFilePath is null) return;

        try
        {
            string line = $"[{DateTimeOffset.Now:O}] [PID {Environment.ProcessId}] [Thread {Environment.CurrentManagedThreadId}] {message}{Environment.NewLine}";
            lock (SyncRoot)
            {
                File.AppendAllText(logFilePath, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Startup-Diagnose darf den Anwendungsstart niemals beeinflussen.
        }
    }
}
