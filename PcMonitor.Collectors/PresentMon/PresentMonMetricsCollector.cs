using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using PcMonitor.Core.Collectors;
using PcMonitor.Core.Metrics;
using PcMonitor.Core.Models;

namespace PcMonitor.Collectors.PresentMon;

public sealed class PresentMonMetricsCollector :
    IMetricCollector,
    IAsyncDisposable
{
    private static readonly HashSet<string>
        ExcludedApplications =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "PC Monitor.exe",
            "PcMonitor.App.exe",
            "dwm.exe",
            "explorer.exe",
            "SearchHost.exe",
            "SearchApp.exe",
            "StartMenuExperienceHost.exe",
            "ShellExperienceHost.exe",
            "TextInputHost.exe",
            "LockApp.exe",
            "ApplicationFrameHost.exe"
        };

    private readonly PresentMonOptions _options;
    private readonly object _stateLock = new();

    private Process? _process;
    private Task? _errorReaderTask;

    private string? _captureFilePath;
    private string? _logFilePath;
    private string? _activeSessionName;

    private DateTimeOffset _captureStartedAt;

    public PresentMonMetricsCollector(
        PresentMonOptions? options = null)
    {
        _options =
            options ??
            PresentMonOptions.CreateDefault();
    }

    public string Name =>
        "PresentMon FPS and Frametime";

    public bool IsRunning { get; private set; }

    public bool IsAvailable =>
        File.Exists(_options.ExecutablePath);

    public event EventHandler<IMetricSample>?
        SampleCollected;

    public async Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return;
        }

        string captureFilePath;
        string logFilePath;
        string sessionName;

        lock (_stateLock)
        {
            if (IsRunning)
            {
                return;
            }

            string workingDirectory =
                Path.Combine(
                    Path.GetTempPath(),
                    "PcMonitor",
                    "PresentMon");

            Directory.CreateDirectory(
                workingDirectory);

            string identifier =
                $"{Environment.ProcessId}_" +
                Guid.NewGuid().ToString("N");

            captureFilePath =
                Path.Combine(
                    workingDirectory,
                    $"capture_{identifier}.csv");

            logFilePath =
                Path.Combine(
                    workingDirectory,
                    $"presentmon_{identifier}.log");

            sessionName =
                $"{_options.SessionNamePrefix}_" +
                identifier;

            _captureFilePath =
                captureFilePath;

            _logFilePath =
                logFilePath;

            _activeSessionName =
                sessionName;

            _captureStartedAt =
                DateTimeOffset.UtcNow;
        }

        // Sitzung der älteren Implementierung entfernen.
        await TerminateSessionAsync(
            _options.ExecutablePath,
            _options.SessionNamePrefix);

        var startInfo =
            new ProcessStartInfo
            {
                FileName =
                    _options.ExecutablePath,

                Arguments =
                    BuildCaptureArguments(
                        captureFilePath,
                        sessionName),

                WorkingDirectory =
                    Path.GetDirectoryName(
                        _options.ExecutablePath)
                    ?? AppContext.BaseDirectory,

                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = true
            };

        var process =
            new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

        if (!process.Start())
        {
            process.Dispose();

            throw new InvalidOperationException(
                "PresentMon konnte nicht gestartet werden.");
        }

        lock (_stateLock)
        {
            _process = process;
            IsRunning = true;

            _errorReaderTask =
                Task.Run(
                    () => ReadErrorOutputAsync(
                        process,
                        logFilePath),
                    CancellationToken.None);
        }
    }

    public async Task StopAsync()
    {
        Process? process;
        Task? errorReaderTask;

        string? captureFilePath;
        string? logFilePath;
        string? activeSessionName;
        DateTimeOffset captureStartedAt;

        lock (_stateLock)
        {
            process = _process;
            errorReaderTask =
                _errorReaderTask;

            captureFilePath =
                _captureFilePath;

            logFilePath =
                _logFilePath;

            activeSessionName =
                _activeSessionName;

            captureStartedAt =
                _captureStartedAt;

            _process = null;
            _errorReaderTask = null;
            _captureFilePath = null;
            _logFilePath = null;
            _activeSessionName = null;

            IsRunning = false;
        }

        // Wichtig: Zuerst die ETW-Sitzung regulär beenden.
        // Erst dadurch finalisiert PresentMon die CSV-Datei.
        if (!string.IsNullOrWhiteSpace(
                activeSessionName))
        {
            await TerminateSessionAsync(
                _options.ExecutablePath,
                activeSessionName);
        }

        if (process is not null)
        {
            await WaitForProcessExitAsync(
                process,
                _options.EffectiveShutdownTimeout);
        }

        await IgnoreErrorsAsync(
            errorReaderTask);

        // Alle Frame-Samples werden noch innerhalb von
        // CollectorManager.StopAllAsync() publiziert. Der
        // Storage-Channel ist zu diesem Zeitpunkt weiterhin offen.
        if (!string.IsNullOrWhiteSpace(
                captureFilePath))
        {
            await PublishCapturedFramesAsync(
                captureFilePath,
                captureStartedAt,
                logFilePath);
        }

        process?.Dispose();

        TryDeleteFile(
            captureFilePath);

        DeleteEmptyFile(
            logFilePath);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private string BuildCaptureArguments(
        string captureFilePath,
        string sessionName)
    {
        var arguments =
            new List<string>
            {
                "--output_file",
                Quote(captureFilePath),

                "--no_console_stats",

                "--no_track_gpu",
                "--no_track_input",
                "--no_track_display",

                "--set_circular_buffer_size",
                _options.CircularBufferSize.ToString(),

                "--stop_existing_session",

                "--session_name",
                Quote(sessionName)
            };

        if (_options.ExcludeDroppedFrames)
        {
            arguments.Add(
                "--exclude_dropped");
        }

        return string.Join(
            ' ',
            arguments);
    }

    private async Task PublishCapturedFramesAsync(
        string captureFilePath,
        DateTimeOffset captureStartedAt,
        string? logFilePath)
    {
        if (!File.Exists(
                captureFilePath))
        {
            await AppendLogAsync(
                logFilePath,
                "Nach dem Stoppen wurde keine CSV-Datei gefunden.");

            return;
        }

        var parser =
            new PresentMonCsvParser();

        var samples =
            new List<FrameMetricsSample>();

        try
        {
            using var stream =
                new FileStream(
                    captureFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite |
                    FileShare.Delete);

            using var reader =
                new StreamReader(
                    stream,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true);

            while (await reader.ReadLineAsync()
                   is string line)
            {
                if (parser.TryParseLine(
                        line,
                        captureStartedAt,
                        out FrameMetricsSample? sample) &&
                    sample is not null &&
                    IsEligibleApplication(sample))
                {
                    samples.Add(sample);
                }
            }
        }
        catch (Exception exception)
        {
            await AppendLogAsync(
                logFilePath,
                "CSV konnte nicht gelesen werden: " +
                exception);

            return;
        }

        if (samples.Count == 0)
        {
            await AppendLogAsync(
                logFilePath,
                "Die CSV enthielt keine geeigneten Frame-Samples.");

            return;
        }

        // Alle geeigneten Grafikprozesse speichern.
        // Die eigentliche Spielauswahl erfolgt später gemeinsam
        // mit den ProcessMetricSamples in der Run-Auswertung.
        foreach (FrameMetricsSample sample
                 in samples.OrderBy(sample =>
                     sample.Timestamp))
        {
            SampleCollected?.Invoke(
                this,
                sample);
        }
    }

    private static bool IsEligibleApplication(
        FrameMetricsSample sample)
    {
        if (sample.ProcessId ==
            Environment.ProcessId)
        {
            return false;
        }

        return !ExcludedApplications.Contains(
            sample.Application);
    }

    private static async Task ReadErrorOutputAsync(
        Process process,
        string? logFilePath)
    {
        try
        {
            while (await process.StandardError
                       .ReadLineAsync()
                   is string line)
            {
                await AppendLogAsync(
                    logFilePath,
                    line);
            }
        }
        catch (Exception exception)
        {
            await AppendLogAsync(
                logFilePath,
                "Fehler beim Lesen von PresentMon stderr: " +
                exception.Message);
        }
    }

    private static async Task WaitForProcessExitAsync(
        Process process,
        TimeSpan timeout)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            using var timeoutCancellation =
                new CancellationTokenSource(
                    timeout);

            try
            {
                await process.WaitForExitAsync(
                    timeoutCancellation.Token);

                return;
            }
            catch (OperationCanceledException)
            {
                // Danach als letzter Ausweg hart beenden.
            }

            if (!process.HasExited)
            {
                process.Kill(
                    entireProcessTree: true);

                await process.WaitForExitAsync();
            }
        }
        catch (InvalidOperationException)
        {
            // Prozess ist bereits beendet.
        }
        catch (Win32Exception)
        {
            // Prozess endete während der Operation.
        }
    }

    private static async Task TerminateSessionAsync(
        string executablePath,
        string sessionName)
    {
        if (!File.Exists(
                executablePath) ||
            string.IsNullOrWhiteSpace(
                sessionName))
        {
            return;
        }

        var startInfo =
            new ProcessStartInfo
            {
                FileName =
                    executablePath,

                Arguments =
                    "--terminate_existing_session " +
                    "--session_name " +
                    Quote(sessionName),

                WorkingDirectory =
                    Path.GetDirectoryName(
                        executablePath)
                    ?? AppContext.BaseDirectory,

                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

        using var cleanupProcess =
            new Process
            {
                StartInfo = startInfo
            };

        try
        {
            if (!cleanupProcess.Start())
            {
                return;
            }

            using var timeout =
                new CancellationTokenSource(
                    TimeSpan.FromSeconds(5));

            try
            {
                await cleanupProcess.WaitForExitAsync(
                    timeout.Token);
            }
            catch (OperationCanceledException)
            {
                if (!cleanupProcess.HasExited)
                {
                    cleanupProcess.Kill(
                        entireProcessTree: true);
                }
            }
        }
        catch
        {
            // Cleanup darf den normalen Stop nicht verhindern.
        }
    }

    private static async Task AppendLogAsync(
        string? logFilePath,
        string message)
    {
        if (string.IsNullOrWhiteSpace(
                logFilePath))
        {
            return;
        }

        try
        {
            string line =
                $"[{DateTimeOffset.Now:O}] " +
                message +
                Environment.NewLine;

            await File.AppendAllTextAsync(
                logFilePath,
                line,
                Encoding.UTF8);
        }
        catch
        {
            // Logging ist optional.
        }
    }

    private static async Task IgnoreErrorsAsync(
        Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task;
        }
        catch
        {
            // Fehler wurden bereits ins Log geschrieben.
        }
    }

    private static void TryDeleteFile(
        string? filePath)
    {
        if (string.IsNullOrWhiteSpace(
                filePath))
        {
            return;
        }

        try
        {
            File.Delete(
                filePath);
        }
        catch
        {
            // Temporäre Datei kann später entfernt werden.
        }
    }

    private static void DeleteEmptyFile(
        string? filePath)
    {
        if (string.IsNullOrWhiteSpace(
                filePath))
        {
            return;
        }

        try
        {
            var fileInfo =
                new FileInfo(
                    filePath);

            if (fileInfo.Exists &&
                fileInfo.Length == 0)
            {
                fileInfo.Delete();
            }
        }
        catch
        {
            // Diagnose-Log ist optional.
        }
    }

    private static string Quote(
        string value)
    {
        return
            "\"" +
            value.Replace(
                "\"",
                "\\\"") +
            "\"";
    }
}
