using System.Globalization;
using System.Text;
using System.Text.Json;
using PcMonitor.Core.Models;
using PcMonitor.Core.Storage;

namespace PcMonitor.Application.Export;

public sealed class RunExportService : IRunExportService
{
    private readonly IMonitoringStorage _storage;

    private readonly SemaphoreSlim _initializationLock =
        new(1, 1);

    private bool _initialized;

    public RunExportService(
        IMonitoringStorage storage)
    {
        _storage = storage
            ?? throw new ArgumentNullException(
                nameof(storage));
    }

    public async Task ExportCsvAsync(
        Guid runId,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ValidateFilePath(filePath);

        await EnsureInitializedAsync(
            cancellationToken);

        RunSummary run =
            await GetRequiredRunAsync(
                runId,
                cancellationToken);

        IReadOnlyList<SystemMetricsSample> samples =
            await _storage.GetSystemMetricSamplesAsync(
                runId,
                cancellationToken);

        string? directory =
            Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream =
            new FileStream(
                filePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16_384,
                useAsync: true);

        await using var writer =
            new StreamWriter(
                stream,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: true));

        await writer.WriteLineAsync(
            "RunId,RunName,RunStartedAt,RunEndedAt," +
            "SampleTimestamp,CpuUsagePercent," +
            "MemoryUsagePercent,UsedMemoryBytes," +
            "TotalMemoryBytes");

        foreach (SystemMetricsSample sample in samples)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string line = string.Join(
                ",",
                EscapeCsv(run.Id.ToString()),
                EscapeCsv(run.Name),
                EscapeCsv(FormatDateTime(run.StartedAt)),
                EscapeCsv(
                    run.EndedAt is null
                        ? string.Empty
                        : FormatDateTime(run.EndedAt.Value)),
                EscapeCsv(FormatDateTime(sample.Timestamp)),
                sample.CpuUsagePercent.ToString(
                    "F2",
                    CultureInfo.InvariantCulture),
                sample.MemoryUsagePercent.ToString(
                    "F2",
                    CultureInfo.InvariantCulture),
                sample.UsedMemoryBytes.ToString(
                    CultureInfo.InvariantCulture),
                sample.TotalMemoryBytes.ToString(
                    CultureInfo.InvariantCulture));

            await writer.WriteLineAsync(line);
        }
    }

    public async Task ExportJsonAsync(
        Guid runId,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ValidateFilePath(filePath);

        await EnsureInitializedAsync(
            cancellationToken);

        RunSummary run =
            await GetRequiredRunAsync(
                runId,
                cancellationToken);

        IReadOnlyList<SystemMetricsSample> samples =
            await _storage.GetSystemMetricSamplesAsync(
                runId,
                cancellationToken);

        var exportData =
            new RunExportDocument(
                Run: new RunExportInfo(
                    Id: run.Id,
                    Name: run.Name,
                    StartedAt: run.StartedAt,
                    EndedAt: run.EndedAt,
                    Status: run.Status.ToString(),
                    DurationSeconds:
                        run.Duration.TotalSeconds,
                    SampleCount: run.SampleCount),
                SystemMetrics: samples);

        string? directory =
            Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream =
            new FileStream(
                filePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16_384,
                useAsync: true);

        var options =
            new JsonSerializerOptions
            {
                WriteIndented = true
            };

        await JsonSerializer.SerializeAsync(
            stream,
            exportData,
            options,
            cancellationToken);
    }

    private async Task<RunSummary> GetRequiredRunAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        RunSummary? run =
            await _storage.GetRunAsync(
                runId,
                cancellationToken);

        return run
            ?? throw new InvalidOperationException(
                $"Der Run '{runId}' wurde nicht gefunden.");
    }

    private async Task EnsureInitializedAsync(
        CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(
            cancellationToken);

        try
        {
            if (_initialized)
            {
                return;
            }

            await _storage.InitializeAsync(
                cancellationToken);

            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private static string EscapeCsv(
        string value)
    {
        bool mustQuote =
            value.Contains(',') ||
            value.Contains('"') ||
            value.Contains('\r') ||
            value.Contains('\n');

        if (!mustQuote)
        {
            return value;
        }

        return
            $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string FormatDateTime(
        DateTimeOffset value)
    {
        return value
            .ToUniversalTime()
            .ToString(
                "O",
                CultureInfo.InvariantCulture);
    }

    private static void ValidateFilePath(
        string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "Der Dateipfad darf nicht leer sein.",
                nameof(filePath));
        }
    }

    private sealed record RunExportDocument(
        RunExportInfo Run,
        IReadOnlyList<SystemMetricsSample> SystemMetrics);

    private sealed record RunExportInfo(
        Guid Id,
        string Name,
        DateTimeOffset StartedAt,
        DateTimeOffset? EndedAt,
        string Status,
        double DurationSeconds,
        long SampleCount);
}