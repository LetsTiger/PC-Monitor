using System.IO;
using System.Text;

namespace PcMonitor.App.Services;

public static class AppLog
{
    private const long MaximumLogFileSizeBytes =
        2 * 1024 * 1024;

    private const int MaximumArchivedLogFiles = 5;

    private static readonly SemaphoreSlim
        WriteLock = new(1, 1);

    public static string LogDirectoryPath { get; } =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "PcMonitor",
            "Logs");

    public static string CurrentLogFilePath { get; } =
        Path.Combine(
            LogDirectoryPath,
            "pcmonitor.log");

    public static void Information(
        string message)
    {
        Write(
            "INFO",
            message,
            exception: null);
    }

    public static void Warning(
        string message)
    {
        Write(
            "WARN",
            message,
            exception: null);
    }

    public static void Error(
        string message,
        Exception? exception = null)
    {
        Write(
            "ERROR",
            message,
            exception);
    }

    private static void Write(
        string level,
        string message,
        Exception? exception)
    {
        try
        {
            WriteAsync(
                    level,
                    message,
                    exception)
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            // Logging darf die Anwendung niemals beenden.
        }
    }

    private static async Task WriteAsync(
        string level,
        string message,
        Exception? exception)
    {
        await WriteLock.WaitAsync()
            .ConfigureAwait(false);

        try
        {
            Directory.CreateDirectory(
                LogDirectoryPath);

            RotateLogsIfRequired();

            var builder =
                new StringBuilder();

            builder.Append('[')
                .Append(
                    DateTimeOffset.Now.ToString("O"))
                .Append("] [")
                .Append(level)
                .Append("] ")
                .AppendLine(message);

            if (exception is not null)
            {
                builder.AppendLine(
                    exception.ToString());
            }

            await File.AppendAllTextAsync(
                CurrentLogFilePath,
                builder.ToString(),
                Encoding.UTF8)
                .ConfigureAwait(false);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    private static void RotateLogsIfRequired()
    {
        var currentFile =
            new FileInfo(
                CurrentLogFilePath);

        if (!currentFile.Exists ||
            currentFile.Length <
            MaximumLogFileSizeBytes)
        {
            return;
        }

        for (int index =
                 MaximumArchivedLogFiles - 1;
             index >= 1;
             index--)
        {
            string sourcePath =
                Path.Combine(
                    LogDirectoryPath,
                    $"pcmonitor.{index}.log");

            string destinationPath =
                Path.Combine(
                    LogDirectoryPath,
                    $"pcmonitor.{index + 1}.log");

            if (File.Exists(
                    sourcePath))
            {
                File.Move(
                    sourcePath,
                    destinationPath,
                    overwrite: true);
            }
        }

        string firstArchivePath =
            Path.Combine(
                LogDirectoryPath,
                "pcmonitor.1.log");

        File.Move(
            CurrentLogFilePath,
            firstArchivePath,
            overwrite: true);
    }
}
