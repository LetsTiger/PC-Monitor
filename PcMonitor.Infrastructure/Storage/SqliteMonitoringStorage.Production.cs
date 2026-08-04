using System.Text.Json;
using Microsoft.Data.Sqlite;
using PcMonitor.Core.Models;

namespace PcMonitor.Infrastructure.Storage;

public sealed partial class SqliteMonitoringStorage
{
    private static readonly JsonSerializerOptions
        AnalysisJsonOptions =
            new()
            {
                PropertyNameCaseInsensitive = true
            };

    private static async Task InitializeProductionTablesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS RunApplicationOverrides
            (
                RunId                   TEXT NOT NULL PRIMARY KEY,
                MainProcessId           INTEGER NOT NULL,
                MainProcessName         TEXT NOT NULL,
                FrameProcessId          INTEGER NOT NULL,
                FrameApplication        TEXT NOT NULL,
                RememberForFutureRuns   INTEGER NOT NULL,
                UpdatedAt               TEXT NOT NULL,

                FOREIGN KEY (RunId)
                    REFERENCES Runs (Id)
                    ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS PersistedAnalysisResults
            (
                RunId               TEXT NOT NULL PRIMARY KEY,
                AnalysisVersion     TEXT NOT NULL,
                RuleSetVersion      TEXT NOT NULL,
                CreatedAt           TEXT NOT NULL,
                JsonPayload         TEXT NOT NULL,

                FOREIGN KEY (RunId)
                    REFERENCES Runs (Id)
                    ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS ApplicationMappingRules
            (
                MainProcessName     TEXT NOT NULL PRIMARY KEY,
                FrameApplication    TEXT NOT NULL,
                UpdatedAt           TEXT NOT NULL
            );
            """,
            cancellationToken);
        await ExecuteNonQueryAsync(
            connection,
            "PRAGMA user_version = 2;",
            cancellationToken);
    }

    public async Task<RunApplicationOverride?>
        GetRunApplicationOverrideAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection =
            await OpenConnectionAsync(
                cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                MainProcessId,
                MainProcessName,
                FrameProcessId,
                FrameApplication,
                RememberForFutureRuns,
                UpdatedAt
            FROM RunApplicationOverrides
            WHERE RunId = $runId;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        if (!await reader.ReadAsync(
                cancellationToken))
        {
            return null;
        }

        return new RunApplicationOverride(
            RunId: runId,
            MainProcessId:
                reader.GetInt32(
                    reader.GetOrdinal("MainProcessId")),
            MainProcessName:
                reader.GetString(
                    reader.GetOrdinal("MainProcessName")),
            FrameProcessId:
                reader.GetInt32(
                    reader.GetOrdinal("FrameProcessId")),
            FrameApplication:
                reader.GetString(
                    reader.GetOrdinal("FrameApplication")),
            RememberForFutureRuns:
                reader.GetInt32(
                    reader.GetOrdinal(
                        "RememberForFutureRuns")) == 1,
            UpdatedAt:
                ParseDateTime(
                    reader.GetString(
                        reader.GetOrdinal("UpdatedAt"))));
    }

    public async Task SaveRunApplicationOverrideAsync(
        RunApplicationOverride value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        await using SqliteConnection connection =
            await OpenConnectionAsync(
                cancellationToken);

        await using SqliteTransaction transaction =
            (SqliteTransaction)
            await connection.BeginTransactionAsync(
                cancellationToken);

        try
        {
            await using (
                SqliteCommand command =
                    connection.CreateCommand())
            {
                command.Transaction =
                    transaction;

                command.CommandText =
                    """
                    INSERT INTO RunApplicationOverrides
                    (
                        RunId,
                        MainProcessId,
                        MainProcessName,
                        FrameProcessId,
                        FrameApplication,
                        RememberForFutureRuns,
                        UpdatedAt
                    )
                    VALUES
                    (
                        $runId,
                        $mainProcessId,
                        $mainProcessName,
                        $frameProcessId,
                        $frameApplication,
                        $rememberForFutureRuns,
                        $updatedAt
                    )
                    ON CONFLICT(RunId)
                    DO UPDATE SET
                        MainProcessId =
                            excluded.MainProcessId,
                        MainProcessName =
                            excluded.MainProcessName,
                        FrameProcessId =
                            excluded.FrameProcessId,
                        FrameApplication =
                            excluded.FrameApplication,
                        RememberForFutureRuns =
                            excluded.RememberForFutureRuns,
                        UpdatedAt =
                            excluded.UpdatedAt;
                    """;

                command.Parameters.AddWithValue(
                    "$runId",
                    value.RunId.ToString());

                command.Parameters.AddWithValue(
                    "$mainProcessId",
                    value.MainProcessId);

                command.Parameters.AddWithValue(
                    "$mainProcessName",
                    value.MainProcessName);

                command.Parameters.AddWithValue(
                    "$frameProcessId",
                    value.FrameProcessId);

                command.Parameters.AddWithValue(
                    "$frameApplication",
                    value.FrameApplication);

                command.Parameters.AddWithValue(
                    "$rememberForFutureRuns",
                    value.RememberForFutureRuns
                        ? 1
                        : 0);

                command.Parameters.AddWithValue(
                    "$updatedAt",
                    FormatDateTime(
                        value.UpdatedAt));

                await command.ExecuteNonQueryAsync(
                    cancellationToken);
            }

            if (value.RememberForFutureRuns)
            {
                await using SqliteCommand ruleCommand =
                    connection.CreateCommand();

                ruleCommand.Transaction =
                    transaction;

                ruleCommand.CommandText =
                    """
                    INSERT INTO ApplicationMappingRules
                    (
                        MainProcessName,
                        FrameApplication,
                        UpdatedAt
                    )
                    VALUES
                    (
                        $mainProcessName,
                        $frameApplication,
                        $updatedAt
                    )
                    ON CONFLICT(MainProcessName)
                    DO UPDATE SET
                        FrameApplication =
                            excluded.FrameApplication,
                        UpdatedAt =
                            excluded.UpdatedAt;
                    """;

                ruleCommand.Parameters.AddWithValue(
                    "$mainProcessName",
                    value.MainProcessName);

                ruleCommand.Parameters.AddWithValue(
                    "$frameApplication",
                    value.FrameApplication);

                ruleCommand.Parameters.AddWithValue(
                    "$updatedAt",
                    FormatDateTime(
                        value.UpdatedAt));

                await ruleCommand.ExecuteNonQueryAsync(
                    cancellationToken);
            }

            await transaction.CommitAsync(
                cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(
                cancellationToken);

            throw;
        }
    }

    public async Task<PersistedAnalysisResult?>
        GetPersistedAnalysisResultAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection =
            await OpenConnectionAsync(
                cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT JsonPayload
            FROM PersistedAnalysisResults
            WHERE RunId = $runId;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        object? value =
            await command.ExecuteScalarAsync(
                cancellationToken);

        if (value is not string json ||
            string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<
            PersistedAnalysisResult>(
                json,
                AnalysisJsonOptions);
    }

    public async Task SavePersistedAnalysisResultAsync(
        PersistedAnalysisResult value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        string json =
            JsonSerializer.Serialize(
                value,
                AnalysisJsonOptions);

        await using SqliteConnection connection =
            await OpenConnectionAsync(
                cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO PersistedAnalysisResults
            (
                RunId,
                AnalysisVersion,
                RuleSetVersion,
                CreatedAt,
                JsonPayload
            )
            VALUES
            (
                $runId,
                $analysisVersion,
                $ruleSetVersion,
                $createdAt,
                $jsonPayload
            )
            ON CONFLICT(RunId)
            DO UPDATE SET
                AnalysisVersion =
                    excluded.AnalysisVersion,
                RuleSetVersion =
                    excluded.RuleSetVersion,
                CreatedAt =
                    excluded.CreatedAt,
                JsonPayload =
                    excluded.JsonPayload;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            value.RunId.ToString());

        command.Parameters.AddWithValue(
            "$analysisVersion",
            value.AnalysisVersion);

        command.Parameters.AddWithValue(
            "$ruleSetVersion",
            value.RuleSetVersion);

        command.Parameters.AddWithValue(
            "$createdAt",
            FormatDateTime(
                value.CreatedAt));

        command.Parameters.AddWithValue(
            "$jsonPayload",
            json);

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    public async Task<int> RecoverInterruptedRunsAsync(
        DateTimeOffset recoveredAt,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection =
            await OpenConnectionAsync(
                cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            UPDATE Runs
            SET
                Status = $completedStatus,
                EndedAt = COALESCE(
                    EndedAt,
                    $recoveredAt)
            WHERE Status = $runningStatus;
            """;

        command.Parameters.AddWithValue(
            "$completedStatus",
            (int)RunStatus.Completed);

        command.Parameters.AddWithValue(
            "$runningStatus",
            (int)RunStatus.Running);

        command.Parameters.AddWithValue(
            "$recoveredAt",
            FormatDateTime(
                recoveredAt));

        return await command.ExecuteNonQueryAsync(
            cancellationToken);
    }
}
