using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PcMonitor.Application.Settings;
using PcMonitor.Core.Models;
using PcMonitor.Core.Storage;

namespace PcMonitor.App.Services;

public sealed class DiagnosticsPackageService :
    IDiagnosticsPackageService
{
    private readonly IApplicationSettingsService
        _settingsService;

    private readonly IMonitoringStorage _storage;

    private readonly string _databasePath;

    public DiagnosticsPackageService(
        IApplicationSettingsService settingsService,
        IMonitoringStorage storage,
        string databasePath)
    {
        _settingsService =
            settingsService ??
            throw new ArgumentNullException(
                nameof(settingsService));

        _storage =
            storage ??
            throw new ArgumentNullException(
                nameof(storage));

        _databasePath =
            Path.GetFullPath(
                databasePath);
    }

    public async Task<string>
        CreateAsync(
            CancellationToken cancellationToken = default)
    {
        string outputDirectory =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments),
                "PC Monitor Diagnostics");

        Directory.CreateDirectory(
            outputDirectory);

        string packagePath =
            Path.Combine(
                outputDirectory,
                $"PC-Monitor-Diagnostics-" +
                $"{DateTime.Now:yyyyMMdd-HHmmss}.zip");

        string workingDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "PcMonitor",
                "Diagnostics",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            workingDirectory);

        try
        {
            await WriteVersionInformationAsync(
                workingDirectory,
                cancellationToken);

            await WriteSanitizedSettingsAsync(
                workingDirectory,
                cancellationToken);

            await WriteRunAndProcessInformationAsync(
                workingDirectory,
                cancellationToken);

            await WriteDatabaseStatisticsAsync(
                workingDirectory,
                cancellationToken);

            CopyApplicationLogs(
                workingDirectory);

            WriteInstalledFiles(
                workingDirectory);

            ZipFile.CreateFromDirectory(
                workingDirectory,
                packagePath,
                CompressionLevel.Optimal,
                includeBaseDirectory: false);

            return packagePath;
        }
        finally
        {
            TryDeleteDirectory(
                workingDirectory);
        }
    }

    private static async Task
        WriteVersionInformationAsync(
            string workingDirectory,
            CancellationToken cancellationToken)
    {
        string presentMonPath =
            Path.Combine(
                AppContext.BaseDirectory,
                "Tools",
                "PresentMon",
                "PresentMon.exe");

        string? presentMonVersion =
            File.Exists(
                presentMonPath)
                ? FileVersionInfo
                    .GetVersionInfo(
                        presentMonPath)
                    .FileVersion
                : null;

        var information =
            new
            {
                Product =
                    ReleaseInfo.ProductName,

                ProductVersion =
                    ReleaseInfo.ProductVersion,

                Publisher =
                    ReleaseInfo.Publisher,

                AnalysisVersion =
                    AnalysisVersionInfo.AnalysisVersion,

                RuleSetVersion =
                    AnalysisVersionInfo.RuleSetVersion,

                DatabaseSchemaVersion =
                    ReleaseInfo.DatabaseSchemaVersion,

                OperatingSystem =
                    RuntimeInformation.OSDescription,

                OperatingSystemArchitecture =
                    RuntimeInformation.OSArchitecture
                        .ToString(),

                ProcessArchitecture =
                    RuntimeInformation.ProcessArchitecture
                        .ToString(),

                DotNetRuntime =
                    RuntimeInformation.FrameworkDescription,

                PresentMonAvailable =
                    File.Exists(
                        presentMonPath),

                PresentMonVersion =
                    string.IsNullOrWhiteSpace(
                        presentMonVersion)
                        ? "Nicht verfügbar"
                        : presentMonVersion,

                CreatedAt =
                    DateTimeOffset.Now
            };

        await WriteJsonAsync(
            Path.Combine(
                workingDirectory,
                "versions.json"),
            information,
            cancellationToken);
    }

    private async Task WriteSanitizedSettingsAsync(
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        PcMonitorSettings settings =
            await _settingsService.LoadAsync(
                cancellationToken);

        var sanitized =
            new
            {
                settings.SystemIntervalMilliseconds,
                settings.NetworkIntervalMilliseconds,
                settings.HardwareIntervalMilliseconds,
                settings.DiskIntervalMilliseconds,
                settings.ProcessIntervalMilliseconds,
                settings.MaximumProcessesPerSample,
                settings.PresentMonEnabled,
                settings.PresentMonCircularBufferSize,
                settings.TargetFramesPerSecond,
                settings.StutterThresholdMilliseconds,
                settings.MaximumHistoryRuns,

                DatabasePath =
                    SanitizePath(
                        settings.DatabasePath),

                settings.ExcludedFrameApplications
            };

        await WriteJsonAsync(
            Path.Combine(
                workingDirectory,
                "settings-sanitized.json"),
            sanitized,
            cancellationToken);
    }

    private async Task
        WriteRunAndProcessInformationAsync(
            string workingDirectory,
            CancellationToken cancellationToken)
    {
        IReadOnlyList<RunSummary> runs =
            await _storage.GetRunsAsync(
                cancellationToken);

        var runItems =
            new List<object>();

        var processNames =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var executablePaths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (RunSummary run in runs)
        {
            runItems.Add(
                new
                {
                    run.Id,
                    run.Name,
                    run.StartedAt,
                    run.EndedAt,
                    Status =
                        run.Status.ToString(),

                    run.SampleCount
                });

            IReadOnlyList<ProcessRunAnalysis>
                processAnalyses =
                    await _storage
                        .GetProcessRunAnalysesAsync(
                            run.Id,
                            cancellationToken);

            foreach (ProcessRunAnalysis process
                     in processAnalyses)
            {
                if (!string.IsNullOrWhiteSpace(
                        process.ProcessName))
                {
                    processNames.Add(
                        process.ProcessName);
                }

                if (!string.IsNullOrWhiteSpace(
                        process.ExecutablePath))
                {
                    executablePaths.Add(
                        SanitizePath(
                            process.ExecutablePath));
                }
            }
        }

        await WriteJsonAsync(
            Path.Combine(
                workingDirectory,
                "runs-and-processes.json"),
            new
            {
                Runs = runItems,
                ProcessNames =
                    processNames
                        .OrderBy(value => value)
                        .ToArray(),
                ExecutablePaths =
                    executablePaths
                        .OrderBy(value => value)
                        .ToArray()
            },
            cancellationToken);
    }

    private async Task WriteDatabaseStatisticsAsync(
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(
                _databasePath))
        {
            await WriteJsonAsync(
                Path.Combine(
                    workingDirectory,
                    "database-statistics.json"),
                new
                {
                    DatabaseAvailable = false
                },
                cancellationToken);

            return;
        }

        var tableCounts =
            new Dictionary<string, long>(
                StringComparer.OrdinalIgnoreCase);

        var connectionString =
            new SqliteConnectionStringBuilder
            {
                DataSource =
                    _databasePath,

                Mode =
                    SqliteOpenMode.ReadOnly
            }.ToString();

        await using var connection =
            new SqliteConnection(
                connectionString);

        await connection.OpenAsync(
            cancellationToken);

        string[] allowedTables =
        [
            "Runs",
            "SystemMetricSamples",
            "NetworkMetricSamples",
            "HardwareMetricSamples",
            "DiskMetricSamples",
            "ProcessMetricSamples",
            "CpuCoreMetricSamples",
            "FrameMetricSamples",
            "NetworkIncidents",
            "RunApplicationOverrides",
            "PersistedAnalysisResults",
            "ApplicationMappingRules"
        ];

        foreach (string table in allowedTables)
        {
            await using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                $"SELECT COUNT(*) FROM \"{table}\";";

            try
            {
                object? result =
                    await command.ExecuteScalarAsync(
                        cancellationToken);

                tableCounts[table] =
                    Convert.ToInt64(
                        result);
            }
            catch (SqliteException)
            {
                // Tabelle existiert bei älteren Datenbanken
                // eventuell noch nicht.
            }
        }

        await WriteJsonAsync(
            Path.Combine(
                workingDirectory,
                "database-statistics.json"),
            new
            {
                DatabaseAvailable = true,
                DatabasePath =
                    SanitizePath(
                        _databasePath),
                TableCounts =
                    tableCounts
            },
            cancellationToken);
    }

    private static void CopyApplicationLogs(
        string workingDirectory)
    {
        if (!Directory.Exists(
                AppLog.LogDirectoryPath))
        {
            return;
        }

        string targetDirectory =
            Path.Combine(
                workingDirectory,
                "logs");

        Directory.CreateDirectory(
            targetDirectory);

        foreach (string sourcePath
                 in Directory.EnumerateFiles(
                     AppLog.LogDirectoryPath,
                     "*.log",
                     SearchOption.TopDirectoryOnly))
        {
            File.Copy(
                sourcePath,
                Path.Combine(
                    targetDirectory,
                    Path.GetFileName(
                        sourcePath)),
                overwrite: true);
        }
    }

    private static void WriteInstalledFiles(
        string workingDirectory)
    {
        var files =
            new List<object>();

        foreach (string path
                 in Directory.EnumerateFiles(
                     AppContext.BaseDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            var fileInfo =
                new FileInfo(
                    path);

            string? version = null;

            if (string.Equals(
                    fileInfo.Extension,
                    ".exe",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    fileInfo.Extension,
                    ".dll",
                    StringComparison.OrdinalIgnoreCase))
            {
                version =
                    FileVersionInfo
                        .GetVersionInfo(
                            path)
                        .FileVersion;
            }

            files.Add(
                new
                {
                    RelativePath =
                        Path.GetRelativePath(
                            AppContext.BaseDirectory,
                            path),

                    fileInfo.Length,
                    Version =
                        version
                });
        }

        File.WriteAllText(
            Path.Combine(
                workingDirectory,
                "installed-files.json"),
            JsonSerializer.Serialize(
                files,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
    }

    private static string SanitizePath(
        string? path)
    {
        if (string.IsNullOrWhiteSpace(
                path))
        {
            return string.Empty;
        }

        string userProfile =
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);

        return path.Replace(
            userProfile,
            "%USERPROFILE%",
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteJsonAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        await using FileStream stream =
            File.Create(
                path);

        await JsonSerializer.SerializeAsync(
            stream,
            value,
            new JsonSerializerOptions
            {
                WriteIndented = true
            },
            cancellationToken);
    }

    private static void TryDeleteDirectory(
        string path)
    {
        try
        {
            if (Directory.Exists(
                    path))
            {
                Directory.Delete(
                    path,
                    recursive: true);
            }
        }
        catch
        {
            // Temporäre Diagnoseinhalte können später
            // durch Windows entfernt werden.
        }
    }
}
