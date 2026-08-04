using System.Globalization;
using Microsoft.Data.Sqlite;
using PcMonitor.Core.Metrics;
using PcMonitor.Core.Models;
using PcMonitor.Core.Storage;
using PcMonitor.Core.Diagnostics;
using PcMonitor.Core.Timeline;

namespace PcMonitor.Infrastructure.Storage;

public sealed partial class SqliteMonitoringStorage : IMonitoringStorage
{
    private readonly string _connectionString;
    private readonly string _databasePath;

    public SqliteMonitoringStorage(
        MonitoringDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _databasePath = options.DatabasePath;

        var connectionStringBuilder =
            new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            };

        _connectionString =
            connectionStringBuilder.ToString();
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureDatabaseDirectoryExists();

        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            PRAGMA journal_mode = WAL;
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            PRAGMA foreign_keys = ON;
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS Runs
            (
                Id          TEXT NOT NULL PRIMARY KEY,
                Name        TEXT NOT NULL,
                StartedAt   TEXT NOT NULL,
                EndedAt     TEXT NULL,
                Status      INTEGER NOT NULL
            );
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS SystemMetricSamples
            (
                Id                  INTEGER NOT NULL
                                    PRIMARY KEY AUTOINCREMENT,

                RunId               TEXT NOT NULL,
                Timestamp           TEXT NOT NULL,

                CpuUsagePercent     REAL NOT NULL,
                MemoryUsagePercent  REAL NOT NULL,
                UsedMemoryBytes     INTEGER NOT NULL,
                TotalMemoryBytes    INTEGER NOT NULL,

                FOREIGN KEY (RunId)
                    REFERENCES Runs (Id)
                    ON DELETE CASCADE
            );
            """,
            cancellationToken);
        
        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS NetworkMetricSamples
            (
                Id                          INTEGER NOT NULL
                                            PRIMARY KEY AUTOINCREMENT,

                RunId                       TEXT NOT NULL,
                Timestamp                   TEXT NOT NULL,

                GatewayAddress              TEXT NULL,
                GatewaySuccess              INTEGER NOT NULL,
                GatewayLatencyMilliseconds  INTEGER NULL,
                GatewayPacketLossPercent    REAL NOT NULL DEFAULT 0,
                GatewayJitterMilliseconds   REAL NULL,
                GatewayConsecutiveFailures  INTEGER NOT NULL DEFAULT 0,

                InternetTarget              TEXT NOT NULL,
                InternetSuccess             INTEGER NOT NULL,
                InternetLatencyMilliseconds INTEGER NULL,
                InternetPacketLossPercent   REAL NOT NULL DEFAULT 0,
                InternetJitterMilliseconds  REAL NULL,
                InternetConsecutiveFailures INTEGER NOT NULL DEFAULT 0,

                FOREIGN KEY (RunId)
                    REFERENCES Runs (Id)
                    ON DELETE CASCADE
            );
            """,
            cancellationToken);
        
        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS HardwareMetricSamples
            (
                Id                              INTEGER NOT NULL
                                                PRIMARY KEY AUTOINCREMENT,

                RunId                           TEXT NOT NULL,
                Timestamp                       TEXT NOT NULL,

                CpuName                         TEXT NULL,
                CpuTemperatureCelsius           REAL NULL,
                CpuMaximumCoreTemperatureCelsius REAL NULL,
                CpuAverageClockMegahertz        REAL NULL,
                CpuPackagePowerWatts            REAL NULL,

                GpuName                         TEXT NULL,
                GpuLoadPercent                  REAL NULL,
                GpuTemperatureCelsius           REAL NULL,
                GpuHotspotTemperatureCelsius    REAL NULL,
                GpuCoreClockMegahertz           REAL NULL,
                GpuMemoryClockMegahertz         REAL NULL,
                GpuPowerWatts                   REAL NULL,
                GpuUsedMemoryMegabytes          REAL NULL,
                GpuTotalMemoryMegabytes         REAL NULL,

                FOREIGN KEY (RunId)
                    REFERENCES Runs (Id)
                    ON DELETE CASCADE
            );
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE INDEX IF NOT EXISTS
                IX_HardwareMetricSamples_RunId_Timestamp
            ON HardwareMetricSamples
                (RunId, Timestamp);
            """,
            cancellationToken);
        
        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS NetworkIncidents
            (
                Id              TEXT NOT NULL PRIMARY KEY,
                RunId           TEXT NOT NULL,
                Target          INTEGER NOT NULL,
                Address         TEXT NULL,
                StartedAt       TEXT NOT NULL,
                EndedAt         TEXT NOT NULL,
                FailureCount    INTEGER NOT NULL,

                FOREIGN KEY (RunId)
                    REFERENCES Runs (Id)
                    ON DELETE CASCADE
            );
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE INDEX IF NOT EXISTS
                IX_NetworkIncidents_RunId_StartedAt
            ON NetworkIncidents
                (RunId, StartedAt);
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE INDEX IF NOT EXISTS
                IX_NetworkMetricSamples_RunId_Timestamp
            ON NetworkMetricSamples
                (RunId, Timestamp);
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE INDEX IF NOT EXISTS
                IX_SystemMetricSamples_RunId_Timestamp
            ON SystemMetricSamples
                (RunId, Timestamp);
            """,
            cancellationToken);
        
        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS DiskMetricSamples
            (
                Id                      INTEGER NOT NULL
                                        PRIMARY KEY AUTOINCREMENT,

                RunId                   TEXT NOT NULL,
                Timestamp               TEXT NOT NULL,

                DeviceId                TEXT NOT NULL,
                DeviceName              TEXT NOT NULL,

                TemperatureCelsius      REAL NULL,

                ReadActivityPercent     REAL NULL,
                WriteActivityPercent    REAL NULL,
                TotalActivityPercent    REAL NULL,

                ReadBytesPerSecond      REAL NULL,
                WriteBytesPerSecond     REAL NULL,

                UsedSpacePercent        REAL NULL,
                FreeSpaceGigabytes      REAL NULL,
                TotalSpaceGigabytes     REAL NULL,

                RemainingLifePercent    REAL NULL,

                FOREIGN KEY (RunId)
                    REFERENCES Runs (Id)
                    ON DELETE CASCADE
            );
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE INDEX IF NOT EXISTS
                IX_DiskMetricSamples_RunId_Timestamp
            ON DiskMetricSamples
                (RunId, Timestamp);
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE INDEX IF NOT EXISTS
                IX_DiskMetricSamples_RunId_DeviceId
            ON DiskMetricSamples
                (RunId, DeviceId);
            """,
            cancellationToken);
        
        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS ProcessMetricSamples
            (
                Id                      INTEGER NOT NULL
                                        PRIMARY KEY AUTOINCREMENT,

                RunId                   TEXT NOT NULL,
                Timestamp               TEXT NOT NULL,

                ProcessId               INTEGER NOT NULL,
                ProcessName             TEXT NOT NULL,
                ExecutablePath          TEXT NULL,
                IsForegroundProcess     INTEGER NOT NULL,

                CpuUsagePercent         REAL NOT NULL,

                WorkingSetBytes         INTEGER NOT NULL,
                PrivateMemoryBytes      INTEGER NOT NULL,

                ThreadCount             INTEGER NOT NULL,
                HandleCount             INTEGER NOT NULL,

                ReadBytesPerSecond      REAL NULL,
                WriteBytesPerSecond     REAL NULL,

                FOREIGN KEY (RunId)
                    REFERENCES Runs (Id)
                    ON DELETE CASCADE
            );
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE INDEX IF NOT EXISTS
                IX_ProcessMetricSamples_RunId_Timestamp
            ON ProcessMetricSamples
                (RunId, Timestamp);
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE INDEX IF NOT EXISTS
                IX_ProcessMetricSamples_RunId_ProcessName
            ON ProcessMetricSamples
                (RunId, ProcessName);
            """,
            cancellationToken);
        
        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS CpuCoreMetricSamples
            (
                Id              INTEGER NOT NULL
                                PRIMARY KEY AUTOINCREMENT,

                RunId           TEXT NOT NULL,
                Timestamp       TEXT NOT NULL,

                CoreIndex       INTEGER NOT NULL,
                ThreadIndex     INTEGER NOT NULL,
                SensorName      TEXT NOT NULL,
                LoadPercent     REAL NOT NULL,

                FOREIGN KEY (RunId)
                    REFERENCES Runs (Id)
                    ON DELETE CASCADE
            );
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE INDEX IF NOT EXISTS
                IX_CpuCoreMetricSamples_RunId_Timestamp
            ON CpuCoreMetricSamples
                (RunId, Timestamp);
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE INDEX IF NOT EXISTS
                IX_CpuCoreMetricSamples_RunId_Core_Thread
            ON CpuCoreMetricSamples
                (RunId, CoreIndex, ThreadIndex);
            """,
            cancellationToken);
        
        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS FrameMetricSamples
            (
                Id                          INTEGER NOT NULL
                                            PRIMARY KEY AUTOINCREMENT,
                RunId                       TEXT NOT NULL,
                Timestamp                   TEXT NOT NULL,
                Application                 TEXT NOT NULL,
                ProcessId                   INTEGER NOT NULL,
                PresentRuntime              TEXT NULL,
                PresentMode                 TEXT NULL,
                FrameTimeMilliseconds       REAL NOT NULL,
                FramesPerSecond             REAL NOT NULL,
                CpuBusyMilliseconds         REAL NULL,
                GpuTimeMilliseconds         REAL NULL,
                DisplayLatencyMilliseconds  REAL NULL,
                FOREIGN KEY (RunId)
                    REFERENCES Runs (Id)
                    ON DELETE CASCADE
            );
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE INDEX IF NOT EXISTS
                IX_FrameMetricSamples_RunId_Timestamp
            ON FrameMetricSamples
                (RunId, Timestamp);
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE INDEX IF NOT EXISTS
                IX_FrameMetricSamples_RunId_Application_ProcessId
            ON FrameMetricSamples
                (RunId, Application, ProcessId);
            """,
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            tableName: "NetworkMetricSamples",
            columnName: "GatewayPacketLossPercent",
            columnDefinition:
            "REAL NOT NULL DEFAULT 0",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            tableName: "NetworkMetricSamples",
            columnName: "GatewayJitterMilliseconds",
            columnDefinition:
            "REAL NULL",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            tableName: "NetworkMetricSamples",
            columnName: "GatewayConsecutiveFailures",
            columnDefinition:
            "INTEGER NOT NULL DEFAULT 0",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            tableName: "NetworkMetricSamples",
            columnName: "InternetPacketLossPercent",
            columnDefinition:
            "REAL NOT NULL DEFAULT 0",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            tableName: "NetworkMetricSamples",
            columnName: "InternetJitterMilliseconds",
            columnDefinition:
            "REAL NULL",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            tableName: "NetworkMetricSamples",
            columnName: "InternetConsecutiveFailures",
            columnDefinition:
            "INTEGER NOT NULL DEFAULT 0",
            cancellationToken);

        await InitializeProductionTablesAsync(
            connection,
            cancellationToken);
    }
    
    private static async Task EnsureColumnExistsAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand checkCommand =
            connection.CreateCommand();

        checkCommand.CommandText =
            $"PRAGMA table_info(\"{tableName}\");";

        bool columnExists = false;

        await using (
            SqliteDataReader reader =
            await checkCommand.ExecuteReaderAsync(
                cancellationToken))
        {
            while (await reader.ReadAsync(
                       cancellationToken))
            {
                string existingColumnName =
                    reader.GetString(
                        reader.GetOrdinal("name"));

                if (string.Equals(
                        existingColumnName,
                        columnName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    columnExists = true;
                    break;
                }
            }
        }

        if (columnExists)
        {
            return;
        }

        await using SqliteCommand alterCommand =
            connection.CreateCommand();

        alterCommand.CommandText =
            $"""
             ALTER TABLE "{tableName}"
             ADD COLUMN "{columnName}" {columnDefinition};
             """;

        await alterCommand.ExecuteNonQueryAsync(
            cancellationToken);
    }

    public async Task CreateRunAsync(
        MonitoringRun run,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO Runs
            (
                Id,
                Name,
                StartedAt,
                EndedAt,
                Status
            )
            VALUES
            (
                $id,
                $name,
                $startedAt,
                $endedAt,
                $status
            );
            """;

        command.Parameters.AddWithValue(
            "$id",
            run.Id.ToString());

        command.Parameters.AddWithValue(
            "$name",
            run.Name);

        command.Parameters.AddWithValue(
            "$startedAt",
            FormatDateTime(run.StartedAt));

        command.Parameters.AddWithValue(
            "$endedAt",
            DBNull.Value);

        command.Parameters.AddWithValue(
            "$status",
            (int)run.Status);

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    public async Task CompleteRunAsync(
        MonitoringRun run,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            UPDATE Runs
            SET
                Name = $name,
                EndedAt = $endedAt,
                Status = $status
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue(
            "$id",
            run.Id.ToString());

        command.Parameters.AddWithValue(
            "$name",
            run.Name);

        command.Parameters.AddWithValue(
            "$endedAt",
            run.EndedAt is null
                ? DBNull.Value
                : FormatDateTime(run.EndedAt.Value));

        command.Parameters.AddWithValue(
            "$status",
            (int)run.Status);

        int affectedRows =
            await command.ExecuteNonQueryAsync(
                cancellationToken);

        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                $"Der Run '{run.Id}' wurde in der Datenbank nicht gefunden.");
        }
    }

    public Task SaveSampleAsync(
        Guid runId,
        IMetricSample sample,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sample);

        return sample switch
        {
            SystemMetricsSample systemSample =>
                SaveSystemMetricsSampleAsync(
                    runId,
                    systemSample,
                    cancellationToken),

            NetworkMetricsSample networkSample =>
                SaveNetworkMetricsSampleAsync(
                    runId,
                    networkSample,
                    cancellationToken),
            
            HardwareMetricsSample hardwareSample =>
                SaveHardwareMetricsSampleAsync(
                    runId,
                    hardwareSample,
                    cancellationToken),
            
            DiskMetricsSample diskSample =>
                SaveDiskMetricsSampleAsync(
                    runId,
                    diskSample,
                    cancellationToken),
            
            ProcessMetricsSample processSample =>
                SaveProcessMetricsSampleAsync(
                    runId,
                    processSample,
                    cancellationToken),
            
            CpuCoreMetricsSample cpuCoreSample =>
                SaveCpuCoreMetricsSampleAsync(
                    runId,
                    cpuCoreSample,
                    cancellationToken),

            FrameMetricsSample frameSample =>
                SaveFrameMetricsSampleAsync(
                    runId,
                    frameSample,
                    cancellationToken),

            _ => throw new NotSupportedException(
                $"Der Messwerttyp '{sample.GetType().Name}' " +
                "wird von SQLite noch nicht unterstützt.")
        };
    }
    
    public async Task<IReadOnlyList<RunSummary>> GetRunsAsync(
    CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                r.Id,
                r.Name,
                r.StartedAt,
                r.EndedAt,
                r.Status,

                (
                    SELECT COUNT(*)
                    FROM SystemMetricSamples systemSamples
                    WHERE systemSamples.RunId = r.Id
                )
                +
                (
                    SELECT COUNT(*)
                    FROM NetworkMetricSamples networkSamples
                    WHERE networkSamples.RunId = r.Id
                )
                +
                (
                    SELECT COUNT(*)
                    FROM HardwareMetricSamples hardwareSamples
                    WHERE hardwareSamples.RunId = r.Id
                )
                +
                (
                    SELECT COUNT(*)
                    FROM DiskMetricSamples diskSamples
                    WHERE diskSamples.RunId = r.Id
                )
                +
                (
                    SELECT COUNT(*)
                    FROM ProcessMetricSamples processSamples
                    WHERE processSamples.RunId = r.Id
                )
                +
                (
                    SELECT COUNT(*)
                    FROM CpuCoreMetricSamples cpuCoreSamples
                    WHERE cpuCoreSamples.RunId = r.Id
                ) AS SampleCount

            FROM Runs r
            ORDER BY r.StartedAt DESC;
            """;

        var runs = new List<RunSummary>();

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            runs.Add(ReadRunSummary(reader));
        }

        return runs;
    }

    public async Task<RunSummary?> GetRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                r.Id,
                r.Name,
                r.StartedAt,
                r.EndedAt,
                r.Status,

                (
                    SELECT COUNT(*)
                    FROM SystemMetricSamples systemSamples
                    WHERE systemSamples.RunId = r.Id
                )
                +
                (
                    SELECT COUNT(*)
                    FROM NetworkMetricSamples networkSamples
                    WHERE networkSamples.RunId = r.Id
                )
                +
                (
                    SELECT COUNT(*)
                    FROM HardwareMetricSamples hardwareSamples
                    WHERE hardwareSamples.RunId = r.Id
                ) 
                +
                (
                    SELECT COUNT(*)
                    FROM DiskMetricSamples diskSamples
                    WHERE diskSamples.RunId = r.Id
                )
                +
                (
                    SELECT COUNT(*)
                    FROM ProcessMetricSamples processSamples
                    WHERE processSamples.RunId = r.Id
                )
                +
                (
                    SELECT COUNT(*)
                    FROM CpuCoreMetricSamples cpuCoreSamples
                    WHERE cpuCoreSamples.RunId = r.Id
                ) AS SampleCount

            FROM Runs r
            WHERE r.Id = $runId;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadRunSummary(reader);
    }
    
    public async Task<IReadOnlyList<SystemMetricsSample>>
        GetSystemMetricSamplesAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                Timestamp,
                CpuUsagePercent,
                MemoryUsagePercent,
                UsedMemoryBytes,
                TotalMemoryBytes
            FROM SystemMetricSamples
            WHERE RunId = $runId
            ORDER BY Timestamp ASC;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        var samples =
            new List<SystemMetricsSample>();

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            string timestampText =
                reader.GetString(
                    reader.GetOrdinal("Timestamp"));

            double cpuUsage =
                reader.GetDouble(
                    reader.GetOrdinal("CpuUsagePercent"));

            double memoryUsage =
                reader.GetDouble(
                    reader.GetOrdinal("MemoryUsagePercent"));

            long usedMemory =
                reader.GetInt64(
                    reader.GetOrdinal("UsedMemoryBytes"));

            long totalMemory =
                reader.GetInt64(
                    reader.GetOrdinal("TotalMemoryBytes"));

            samples.Add(
                new SystemMetricsSample(
                    Timestamp: ParseDateTime(timestampText),
                    CpuUsagePercent: cpuUsage,
                    MemoryUsagePercent: memoryUsage,
                    UsedMemoryBytes: checked((ulong)usedMemory),
                    TotalMemoryBytes: checked((ulong)totalMemory)));
        }

        return samples;
    }
    
    public async Task RenameRunAsync(
        Guid runId,
        string newName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ArgumentException(
                "Der Run-Name darf nicht leer sein.",
                nameof(newName));
        }

        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            UPDATE Runs
            SET Name = $name
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue(
            "$id",
            runId.ToString());

        command.Parameters.AddWithValue(
            "$name",
            newName.Trim());

        int affectedRows =
            await command.ExecuteNonQueryAsync(
                cancellationToken);

        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                $"Der Run '{runId}' wurde nicht gefunden.");
        }
    }

    public async Task DeleteRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(
                cancellationToken);

        try
        {
            await using (
                SqliteCommand deleteSystemSamplesCommand =
                    connection.CreateCommand())
            {
                deleteSystemSamplesCommand.Transaction =
                    transaction;

                deleteSystemSamplesCommand.CommandText =
                    """
                    DELETE FROM SystemMetricSamples
                    WHERE RunId = $runId;
                    """;

                deleteSystemSamplesCommand.Parameters.AddWithValue(
                    "$runId",
                    runId.ToString());

                await deleteSystemSamplesCommand.ExecuteNonQueryAsync(
                    cancellationToken);
            }

            await using (
                SqliteCommand deleteNetworkSamplesCommand =
                    connection.CreateCommand())
            {
                deleteNetworkSamplesCommand.Transaction =
                    transaction;

                deleteNetworkSamplesCommand.CommandText =
                    """
                    DELETE FROM NetworkMetricSamples
                    WHERE RunId = $runId;
                    """;

                deleteNetworkSamplesCommand.Parameters.AddWithValue(
                    "$runId",
                    runId.ToString());

                await deleteNetworkSamplesCommand.ExecuteNonQueryAsync(
                    cancellationToken);
            }

            await using (
                SqliteCommand deleteHardwareSamplesCommand =
                    connection.CreateCommand())
            {
                deleteHardwareSamplesCommand.Transaction =
                    transaction;

                deleteHardwareSamplesCommand.CommandText =
                    """
                    DELETE FROM HardwareMetricSamples
                    WHERE RunId = $runId;
                    """;

                deleteHardwareSamplesCommand.Parameters.AddWithValue(
                    "$runId",
                    runId.ToString());

                await deleteHardwareSamplesCommand.ExecuteNonQueryAsync(
                    cancellationToken);
            }
            
            await using (
                SqliteCommand deleteDiskSamplesCommand =
                connection.CreateCommand())
            {
                deleteDiskSamplesCommand.Transaction =
                    transaction;

                deleteDiskSamplesCommand.CommandText =
                    """
                    DELETE FROM DiskMetricSamples
                    WHERE RunId = $runId;
                    """;

                deleteDiskSamplesCommand.Parameters.AddWithValue(
                    "$runId",
                    runId.ToString());

                await deleteDiskSamplesCommand.ExecuteNonQueryAsync(
                    cancellationToken);
            }
            
            await using (
                SqliteCommand deleteProcessSamplesCommand =
                connection.CreateCommand())
            {
                deleteProcessSamplesCommand.Transaction =
                    transaction;

                deleteProcessSamplesCommand.CommandText =
                    """
                    DELETE FROM ProcessMetricSamples
                    WHERE RunId = $runId;
                    """;

                deleteProcessSamplesCommand.Parameters.AddWithValue(
                    "$runId",
                    runId.ToString());

                await deleteProcessSamplesCommand.ExecuteNonQueryAsync(
                    cancellationToken);
            }
            
            await using (
                SqliteCommand deleteCpuCoreSamplesCommand =
                connection.CreateCommand())
            {
                deleteCpuCoreSamplesCommand.Transaction =
                    transaction;

                deleteCpuCoreSamplesCommand.CommandText =
                    """
                    DELETE FROM CpuCoreMetricSamples
                    WHERE RunId = $runId;
                    """;

                deleteCpuCoreSamplesCommand.Parameters.AddWithValue(
                    "$runId",
                    runId.ToString());

                await deleteCpuCoreSamplesCommand.ExecuteNonQueryAsync(
                    cancellationToken);
            }

            await using (
                SqliteCommand deleteIncidentsCommand =
                    connection.CreateCommand())
            {
                deleteIncidentsCommand.Transaction =
                    transaction;

                deleteIncidentsCommand.CommandText =
                    """
                    DELETE FROM NetworkIncidents
                    WHERE RunId = $runId;
                    """;

                deleteIncidentsCommand.Parameters.AddWithValue(
                    "$runId",
                    runId.ToString());

                await deleteIncidentsCommand.ExecuteNonQueryAsync(
                    cancellationToken);
            }

            int affectedRows;

            await using (
                SqliteCommand deleteRunCommand =
                    connection.CreateCommand())
            {
                deleteRunCommand.Transaction =
                    transaction;

                deleteRunCommand.CommandText =
                    """
                    DELETE FROM Runs
                    WHERE Id = $runId;
                    """;

                deleteRunCommand.Parameters.AddWithValue(
                    "$runId",
                    runId.ToString());

                affectedRows =
                    await deleteRunCommand.ExecuteNonQueryAsync(
                        cancellationToken);
            }

            if (affectedRows != 1)
            {
                throw new InvalidOperationException(
                    $"Der Run '{runId}' wurde nicht gefunden.");
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

    private async Task SaveSystemMetricsSampleAsync(
        Guid runId,
        SystemMetricsSample sample,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO SystemMetricSamples
            (
                RunId,
                Timestamp,
                CpuUsagePercent,
                MemoryUsagePercent,
                UsedMemoryBytes,
                TotalMemoryBytes
            )
            VALUES
            (
                $runId,
                $timestamp,
                $cpuUsagePercent,
                $memoryUsagePercent,
                $usedMemoryBytes,
                $totalMemoryBytes
            );
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        command.Parameters.AddWithValue(
            "$timestamp",
            FormatDateTime(sample.Timestamp));

        command.Parameters.AddWithValue(
            "$cpuUsagePercent",
            sample.CpuUsagePercent);

        command.Parameters.AddWithValue(
            "$memoryUsagePercent",
            sample.MemoryUsagePercent);

        command.Parameters.AddWithValue(
            "$usedMemoryBytes",
            checked((long)sample.UsedMemoryBytes));

        command.Parameters.AddWithValue(
            "$totalMemoryBytes",
            checked((long)sample.TotalMemoryBytes));

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }
    
    private async Task SaveHardwareMetricsSampleAsync(
        Guid runId,
        HardwareMetricsSample sample,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO HardwareMetricSamples
            (
                RunId,
                Timestamp,

                CpuName,
                CpuTemperatureCelsius,
                CpuMaximumCoreTemperatureCelsius,
                CpuAverageClockMegahertz,
                CpuPackagePowerWatts,

                GpuName,
                GpuLoadPercent,
                GpuTemperatureCelsius,
                GpuHotspotTemperatureCelsius,
                GpuCoreClockMegahertz,
                GpuMemoryClockMegahertz,
                GpuPowerWatts,
                GpuUsedMemoryMegabytes,
                GpuTotalMemoryMegabytes
            )
            VALUES
            (
                $runId,
                $timestamp,

                $cpuName,
                $cpuTemperatureCelsius,
                $cpuMaximumCoreTemperatureCelsius,
                $cpuAverageClockMegahertz,
                $cpuPackagePowerWatts,

                $gpuName,
                $gpuLoadPercent,
                $gpuTemperatureCelsius,
                $gpuHotspotTemperatureCelsius,
                $gpuCoreClockMegahertz,
                $gpuMemoryClockMegahertz,
                $gpuPowerWatts,
                $gpuUsedMemoryMegabytes,
                $gpuTotalMemoryMegabytes
            );
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        command.Parameters.AddWithValue(
            "$timestamp",
            FormatDateTime(sample.Timestamp));

        AddNullableParameter(
            command,
            "$cpuName",
            sample.CpuName);

        AddNullableParameter(
            command,
            "$cpuTemperatureCelsius",
            sample.CpuTemperatureCelsius);

        AddNullableParameter(
            command,
            "$cpuMaximumCoreTemperatureCelsius",
            sample.CpuMaximumCoreTemperatureCelsius);

        AddNullableParameter(
            command,
            "$cpuAverageClockMegahertz",
            sample.CpuAverageClockMegahertz);

        AddNullableParameter(
            command,
            "$cpuPackagePowerWatts",
            sample.CpuPackagePowerWatts);

        AddNullableParameter(
            command,
            "$gpuName",
            sample.GpuName);

        AddNullableParameter(
            command,
            "$gpuLoadPercent",
            sample.GpuLoadPercent);

        AddNullableParameter(
            command,
            "$gpuTemperatureCelsius",
            sample.GpuTemperatureCelsius);

        AddNullableParameter(
            command,
            "$gpuHotspotTemperatureCelsius",
            sample.GpuHotspotTemperatureCelsius);

        AddNullableParameter(
            command,
            "$gpuCoreClockMegahertz",
            sample.GpuCoreClockMegahertz);

        AddNullableParameter(
            command,
            "$gpuMemoryClockMegahertz",
            sample.GpuMemoryClockMegahertz);

        AddNullableParameter(
            command,
            "$gpuPowerWatts",
            sample.GpuPowerWatts);

        AddNullableParameter(
            command,
            "$gpuUsedMemoryMegabytes",
            sample.GpuUsedMemoryMegabytes);

        AddNullableParameter(
            command,
            "$gpuTotalMemoryMegabytes",
            sample.GpuTotalMemoryMegabytes);

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }
    
    private async Task SaveNetworkMetricsSampleAsync(
        Guid runId,
        NetworkMetricsSample sample,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO NetworkMetricSamples
            (
                RunId,
                Timestamp,

                GatewayAddress,
                GatewaySuccess,
                GatewayLatencyMilliseconds,
                GatewayPacketLossPercent,
                GatewayJitterMilliseconds,
                GatewayConsecutiveFailures,

                InternetTarget,
                InternetSuccess,
                InternetLatencyMilliseconds,
                InternetPacketLossPercent,
                InternetJitterMilliseconds,
                InternetConsecutiveFailures
            )
            VALUES
            (
                $runId,
                $timestamp,

                $gatewayAddress,
                $gatewaySuccess,
                $gatewayLatencyMilliseconds,
                $gatewayPacketLossPercent,
                $gatewayJitterMilliseconds,
                $gatewayConsecutiveFailures,

                $internetTarget,
                $internetSuccess,
                $internetLatencyMilliseconds,
                $internetPacketLossPercent,
                $internetJitterMilliseconds,
                $internetConsecutiveFailures
            );
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        command.Parameters.AddWithValue(
            "$timestamp",
            FormatDateTime(sample.Timestamp));

        command.Parameters.AddWithValue(
            "$gatewayAddress",
            sample.GatewayAddress is null
                ? DBNull.Value
                : sample.GatewayAddress);

        command.Parameters.AddWithValue(
            "$gatewaySuccess",
            sample.GatewaySuccess ? 1 : 0);

        command.Parameters.AddWithValue(
            "$gatewayLatencyMilliseconds",
            sample.GatewayLatencyMilliseconds is null
                ? DBNull.Value
                : sample.GatewayLatencyMilliseconds.Value);

        command.Parameters.AddWithValue(
            "$gatewayPacketLossPercent",
            sample.GatewayPacketLossPercent);

        command.Parameters.AddWithValue(
            "$gatewayJitterMilliseconds",
            sample.GatewayJitterMilliseconds is null
                ? DBNull.Value
                : sample.GatewayJitterMilliseconds.Value);

        command.Parameters.AddWithValue(
            "$gatewayConsecutiveFailures",
            sample.GatewayConsecutiveFailures);

        command.Parameters.AddWithValue(
            "$internetTarget",
            sample.InternetTarget);

        command.Parameters.AddWithValue(
            "$internetSuccess",
            sample.InternetSuccess ? 1 : 0);

        command.Parameters.AddWithValue(
            "$internetLatencyMilliseconds",
            sample.InternetLatencyMilliseconds is null
                ? DBNull.Value
                : sample.InternetLatencyMilliseconds.Value);

        command.Parameters.AddWithValue(
            "$internetPacketLossPercent",
            sample.InternetPacketLossPercent);

        command.Parameters.AddWithValue(
            "$internetJitterMilliseconds",
            sample.InternetJitterMilliseconds is null
                ? DBNull.Value
                : sample.InternetJitterMilliseconds.Value);

        command.Parameters.AddWithValue(
            "$internetConsecutiveFailures",
            sample.InternetConsecutiveFailures);

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection =
            new SqliteConnection(_connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);

            await using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                PRAGMA foreign_keys = ON;
                PRAGMA busy_timeout = 5000;
                """;

            await command.ExecuteNonQueryAsync(
                cancellationToken);

            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private void EnsureDatabaseDirectoryExists()
    {
        string? directory =
            Path.GetDirectoryName(_databasePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string FormatDateTime(
        DateTimeOffset dateTime)
    {
        return dateTime.ToUniversalTime()
            .ToString(
                "O",
                CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText = commandText;

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }
    
    private static RunSummary ReadRunSummary(
        SqliteDataReader reader)
    {
        string idText =
            reader.GetString(
                reader.GetOrdinal("Id"));

        string name =
            reader.GetString(
                reader.GetOrdinal("Name"));

        string startedAtText =
            reader.GetString(
                reader.GetOrdinal("StartedAt"));

        int endedAtIndex =
            reader.GetOrdinal("EndedAt");

        int statusValue =
            reader.GetInt32(
                reader.GetOrdinal("Status"));

        long sampleCount =
            reader.GetInt64(
                reader.GetOrdinal("SampleCount"));

        DateTimeOffset startedAt =
            ParseDateTime(startedAtText);

        DateTimeOffset? endedAt =
            reader.IsDBNull(endedAtIndex)
                ? null
                : ParseDateTime(
                    reader.GetString(endedAtIndex));

        return new RunSummary(
            Id: Guid.Parse(idText),
            Name: name,
            StartedAt: startedAt,
            EndedAt: endedAt,
            Status: (RunStatus)statusValue,
            SampleCount: sampleCount);
    }

    private static DateTimeOffset ParseDateTime(
        string value)
    {
        return DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
    }

    public async Task<RunAnalysis?> GetRunAnalysisAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        bool runExists =
            await RunExistsAsync(
                connection,
                runId,
                cancellationToken);

        if (!runExists)
        {
            return null;
        }

        SystemAnalysisData systemAnalysis =
            await ReadSystemAnalysisAsync(
                connection,
                runId,
                cancellationToken);

        NetworkAnalysisData networkAnalysis =
            await ReadNetworkAnalysisAsync(
                connection,
                runId,
                cancellationToken);

        HardwareAnalysisData hardwareAnalysis =
            await ReadHardwareAnalysisAsync(
                connection,
                runId,
                cancellationToken);

        return new RunAnalysis(
            RunId: runId,

            SystemSampleCount:
            systemAnalysis.SampleCount,

            AverageCpuUsagePercent:
            systemAnalysis.AverageCpuUsagePercent,

            MaximumCpuUsagePercent:
            systemAnalysis.MaximumCpuUsagePercent,

            AverageMemoryUsagePercent:
            systemAnalysis.AverageMemoryUsagePercent,

            MaximumMemoryUsagePercent:
            systemAnalysis.MaximumMemoryUsagePercent,

            MaximumUsedMemoryBytes:
            systemAnalysis.MaximumUsedMemoryBytes,

            NetworkSampleCount:
            networkAnalysis.SampleCount,

            AverageGatewayLatencyMilliseconds:
            networkAnalysis.AverageGatewayLatencyMilliseconds,

            MaximumGatewayLatencyMilliseconds:
            networkAnalysis.MaximumGatewayLatencyMilliseconds,

            MaximumGatewayPacketLossPercent:
            networkAnalysis.MaximumGatewayPacketLossPercent,

            AverageGatewayJitterMilliseconds:
            networkAnalysis.AverageGatewayJitterMilliseconds,

            GatewayFailureCount:
            networkAnalysis.GatewayFailureCount,

            MaximumGatewayConsecutiveFailures:
            networkAnalysis.MaximumGatewayConsecutiveFailures,

            AverageInternetLatencyMilliseconds:
            networkAnalysis.AverageInternetLatencyMilliseconds,

            MaximumInternetLatencyMilliseconds:
            networkAnalysis.MaximumInternetLatencyMilliseconds,

            MaximumInternetPacketLossPercent:
            networkAnalysis.MaximumInternetPacketLossPercent,

            AverageInternetJitterMilliseconds:
            networkAnalysis.AverageInternetJitterMilliseconds,

            InternetFailureCount:
            networkAnalysis.InternetFailureCount,

            MaximumInternetConsecutiveFailures:
            networkAnalysis.MaximumInternetConsecutiveFailures,

            HardwareSampleCount:
            hardwareAnalysis.SampleCount,

            CpuName:
            hardwareAnalysis.CpuName,

            AverageCpuTemperatureCelsius:
            hardwareAnalysis.AverageCpuTemperatureCelsius,

            MaximumCpuTemperatureCelsius:
            hardwareAnalysis.MaximumCpuTemperatureCelsius,

            MaximumCpuCoreTemperatureCelsius:
            hardwareAnalysis.MaximumCpuCoreTemperatureCelsius,

            AverageCpuClockMegahertz:
            hardwareAnalysis.AverageCpuClockMegahertz,

            MinimumCpuClockMegahertz:
            hardwareAnalysis.MinimumCpuClockMegahertz,

            MaximumCpuClockMegahertz:
            hardwareAnalysis.MaximumCpuClockMegahertz,

            AverageCpuPackagePowerWatts:
            hardwareAnalysis.AverageCpuPackagePowerWatts,

            MaximumCpuPackagePowerWatts:
            hardwareAnalysis.MaximumCpuPackagePowerWatts,

            GpuName:
            hardwareAnalysis.GpuName,

            AverageGpuLoadPercent:
            hardwareAnalysis.AverageGpuLoadPercent,

            MaximumGpuLoadPercent:
            hardwareAnalysis.MaximumGpuLoadPercent,

            AverageGpuTemperatureCelsius:
            hardwareAnalysis.AverageGpuTemperatureCelsius,

            MaximumGpuTemperatureCelsius:
            hardwareAnalysis.MaximumGpuTemperatureCelsius,

            MaximumGpuHotspotTemperatureCelsius:
            hardwareAnalysis.MaximumGpuHotspotTemperatureCelsius,

            AverageGpuCoreClockMegahertz:
            hardwareAnalysis.AverageGpuCoreClockMegahertz,

            MinimumGpuCoreClockMegahertz:
            hardwareAnalysis.MinimumGpuCoreClockMegahertz,

            MaximumGpuCoreClockMegahertz:
            hardwareAnalysis.MaximumGpuCoreClockMegahertz,

            AverageGpuPowerWatts:
            hardwareAnalysis.AverageGpuPowerWatts,

            MaximumGpuPowerWatts:
            hardwareAnalysis.MaximumGpuPowerWatts,

            MaximumGpuUsedMemoryMegabytes:
            hardwareAnalysis.MaximumGpuUsedMemoryMegabytes,

            GpuTotalMemoryMegabytes:
            hardwareAnalysis.GpuTotalMemoryMegabytes);
    }

    public async Task SaveNetworkIncidentAsync(
        NetworkIncident incident,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(incident);

        if (incident.FailureCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(incident),
                "Ein Netzwerkvorfall muss mindestens einen Fehler enthalten.");
        }

        if (incident.EndedAt < incident.StartedAt)
        {
            throw new ArgumentException(
                "Das Ende des Vorfalls darf nicht vor dem Beginn liegen.",
                nameof(incident));
        }

        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO NetworkIncidents
            (
                Id,
                RunId,
                Target,
                Address,
                StartedAt,
                EndedAt,
                FailureCount
            )
            VALUES
            (
                $id,
                $runId,
                $target,
                $address,
                $startedAt,
                $endedAt,
                $failureCount
            );
            """;

        command.Parameters.AddWithValue(
            "$id",
            incident.Id.ToString());

        command.Parameters.AddWithValue(
            "$runId",
            incident.RunId.ToString());

        command.Parameters.AddWithValue(
            "$target",
            (int)incident.Target);

        command.Parameters.AddWithValue(
            "$address",
            incident.Address is null
                ? DBNull.Value
                : incident.Address);

        command.Parameters.AddWithValue(
            "$startedAt",
            FormatDateTime(incident.StartedAt));

        command.Parameters.AddWithValue(
            "$endedAt",
            FormatDateTime(incident.EndedAt));

        command.Parameters.AddWithValue(
            "$failureCount",
            incident.FailureCount);

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }
    
    public async Task<IReadOnlyList<NetworkIncident>>
        GetNetworkIncidentsAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                Id,
                RunId,
                Target,
                Address,
                StartedAt,
                EndedAt,
                FailureCount
            FROM NetworkIncidents
            WHERE RunId = $runId
            ORDER BY StartedAt ASC;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        var incidents =
            new List<NetworkIncident>();

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            int addressOrdinal =
                reader.GetOrdinal("Address");

            incidents.Add(
                new NetworkIncident(
                    Id: Guid.Parse(
                        reader.GetString(
                            reader.GetOrdinal("Id"))),

                    RunId: Guid.Parse(
                        reader.GetString(
                            reader.GetOrdinal("RunId"))),

                    Target: (NetworkIncidentTarget)
                        reader.GetInt32(
                            reader.GetOrdinal("Target")),

                    Address:
                        reader.IsDBNull(addressOrdinal)
                            ? null
                            : reader.GetString(addressOrdinal),

                    StartedAt:
                        ParseDateTime(
                            reader.GetString(
                                reader.GetOrdinal("StartedAt"))),

                    EndedAt:
                        ParseDateTime(
                            reader.GetString(
                                reader.GetOrdinal("EndedAt"))),

                    FailureCount:
                        reader.GetInt32(
                            reader.GetOrdinal("FailureCount"))));
        }

        return incidents;
    }
    
    private static async Task<bool> RunExistsAsync(
        SqliteConnection connection,
        Guid runId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT EXISTS
            (
                SELECT 1
                FROM Runs
                WHERE Id = $runId
            );
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        object? result =
            await command.ExecuteScalarAsync(
                cancellationToken);

        return Convert.ToInt32(result) == 1;
    }

    private static async Task<SystemAnalysisData>
        ReadSystemAnalysisAsync(
            SqliteConnection connection,
            Guid runId,
            CancellationToken cancellationToken)
    {
        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                COUNT(*) AS SampleCount,
                AVG(CpuUsagePercent) AS AverageCpuUsagePercent,
                MAX(CpuUsagePercent) AS MaximumCpuUsagePercent,
                AVG(MemoryUsagePercent) AS AverageMemoryUsagePercent,
                MAX(MemoryUsagePercent) AS MaximumMemoryUsagePercent,
                MAX(UsedMemoryBytes) AS MaximumUsedMemoryBytes
            FROM SystemMetricSamples
            WHERE RunId = $runId;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return SystemAnalysisData.Empty;
        }

        return new SystemAnalysisData(
            SampleCount:
                reader.GetInt64(
                    reader.GetOrdinal("SampleCount")),

            AverageCpuUsagePercent:
                ReadNullableDouble(
                    reader,
                    "AverageCpuUsagePercent"),

            MaximumCpuUsagePercent:
                ReadNullableDouble(
                    reader,
                    "MaximumCpuUsagePercent"),

            AverageMemoryUsagePercent:
                ReadNullableDouble(
                    reader,
                    "AverageMemoryUsagePercent"),

            MaximumMemoryUsagePercent:
                ReadNullableDouble(
                    reader,
                    "MaximumMemoryUsagePercent"),

            MaximumUsedMemoryBytes:
                ReadNullableUInt64(
                    reader,
                    "MaximumUsedMemoryBytes"));
    }

    private static async Task<NetworkAnalysisData>
        ReadNetworkAnalysisAsync(
            SqliteConnection connection,
            Guid runId,
            CancellationToken cancellationToken)
    {
        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                COUNT(*) AS SampleCount,

                AVG(
                    CASE
                        WHEN GatewaySuccess = 1
                        THEN GatewayLatencyMilliseconds
                    END
                ) AS AverageGatewayLatencyMilliseconds,

                MAX(
                    CASE
                        WHEN GatewaySuccess = 1
                        THEN GatewayLatencyMilliseconds
                    END
                ) AS MaximumGatewayLatencyMilliseconds,

                MAX(GatewayPacketLossPercent)
                    AS MaximumGatewayPacketLossPercent,

                AVG(GatewayJitterMilliseconds)
                    AS AverageGatewayJitterMilliseconds,

                SUM(
                    CASE
                        WHEN GatewaySuccess = 0
                        THEN 1
                        ELSE 0
                    END
                ) AS GatewayFailureCount,

                MAX(GatewayConsecutiveFailures)
                    AS MaximumGatewayConsecutiveFailures,

                AVG(
                    CASE
                        WHEN InternetSuccess = 1
                        THEN InternetLatencyMilliseconds
                    END
                ) AS AverageInternetLatencyMilliseconds,

                MAX(
                    CASE
                        WHEN InternetSuccess = 1
                        THEN InternetLatencyMilliseconds
                    END
                ) AS MaximumInternetLatencyMilliseconds,

                MAX(InternetPacketLossPercent)
                    AS MaximumInternetPacketLossPercent,

                AVG(InternetJitterMilliseconds)
                    AS AverageInternetJitterMilliseconds,

                SUM(
                    CASE
                        WHEN InternetSuccess = 0
                        THEN 1
                        ELSE 0
                    END
                ) AS InternetFailureCount,

                MAX(InternetConsecutiveFailures)
                    AS MaximumInternetConsecutiveFailures

            FROM NetworkMetricSamples
            WHERE RunId = $runId;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return NetworkAnalysisData.Empty;
        }

        return new NetworkAnalysisData(
            SampleCount:
                reader.GetInt64(
                    reader.GetOrdinal("SampleCount")),

            AverageGatewayLatencyMilliseconds:
                ReadNullableDouble(
                    reader,
                    "AverageGatewayLatencyMilliseconds"),

            MaximumGatewayLatencyMilliseconds:
                ReadNullableInt64(
                    reader,
                    "MaximumGatewayLatencyMilliseconds"),

            MaximumGatewayPacketLossPercent:
                ReadNullableDouble(
                    reader,
                    "MaximumGatewayPacketLossPercent"),

            AverageGatewayJitterMilliseconds:
                ReadNullableDouble(
                    reader,
                    "AverageGatewayJitterMilliseconds"),

            GatewayFailureCount:
                ReadInt32OrZero(
                    reader,
                    "GatewayFailureCount"),

            MaximumGatewayConsecutiveFailures:
                ReadInt32OrZero(
                    reader,
                    "MaximumGatewayConsecutiveFailures"),

            AverageInternetLatencyMilliseconds:
                ReadNullableDouble(
                    reader,
                    "AverageInternetLatencyMilliseconds"),

            MaximumInternetLatencyMilliseconds:
                ReadNullableInt64(
                    reader,
                    "MaximumInternetLatencyMilliseconds"),

            MaximumInternetPacketLossPercent:
                ReadNullableDouble(
                    reader,
                    "MaximumInternetPacketLossPercent"),

            AverageInternetJitterMilliseconds:
                ReadNullableDouble(
                    reader,
                    "AverageInternetJitterMilliseconds"),

            InternetFailureCount:
                ReadInt32OrZero(
                    reader,
                    "InternetFailureCount"),

            MaximumInternetConsecutiveFailures:
                ReadInt32OrZero(
                    reader,
                    "MaximumInternetConsecutiveFailures"));
    }
    
    private static double? ReadNullableDouble(
        SqliteDataReader reader,
        string columnName)
    {
        int ordinal =
            reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetDouble(ordinal);
    }

    private static long? ReadNullableInt64(
        SqliteDataReader reader,
        string columnName)
    {
        int ordinal =
            reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetInt64(ordinal);
    }

    private static ulong? ReadNullableUInt64(
        SqliteDataReader reader,
        string columnName)
    {
        long? value =
            ReadNullableInt64(
                reader,
                columnName);

        return value is null
            ? null
            : checked((ulong)value.Value);
    }

    private static int ReadInt32OrZero(
        SqliteDataReader reader,
        string columnName)
    {
        int ordinal =
            reader.GetOrdinal(columnName);

        if (reader.IsDBNull(ordinal))
        {
            return 0;
        }

        return checked(
            (int)reader.GetInt64(ordinal));
    }
    
    private static string? ReadNullableString(
        SqliteDataReader reader,
        string columnName)
    {
        int ordinal =
            reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetString(ordinal);
    }
    
    private static double ReadDoubleOrZero(
        SqliteDataReader reader,
        string columnName)
    {
        int ordinal =
            reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? 0
            : reader.GetDouble(ordinal);
    }

    private static long ReadInt64OrZero(
        SqliteDataReader reader,
        string columnName)
    {
        int ordinal =
            reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? 0
            : reader.GetInt64(ordinal);
    }
    
    private readonly record struct SystemAnalysisData(
        long SampleCount,
        double? AverageCpuUsagePercent,
        double? MaximumCpuUsagePercent,
        double? AverageMemoryUsagePercent,
        double? MaximumMemoryUsagePercent,
        ulong? MaximumUsedMemoryBytes)
    {
        public static SystemAnalysisData Empty =>
            new(
                SampleCount: 0,
                AverageCpuUsagePercent: null,
                MaximumCpuUsagePercent: null,
                AverageMemoryUsagePercent: null,
                MaximumMemoryUsagePercent: null,
                MaximumUsedMemoryBytes: null);
    }

    private readonly record struct NetworkAnalysisData(
        long SampleCount,

        double? AverageGatewayLatencyMilliseconds,
        long? MaximumGatewayLatencyMilliseconds,
        double? MaximumGatewayPacketLossPercent,
        double? AverageGatewayJitterMilliseconds,
        int GatewayFailureCount,
        int MaximumGatewayConsecutiveFailures,

        double? AverageInternetLatencyMilliseconds,
        long? MaximumInternetLatencyMilliseconds,
        double? MaximumInternetPacketLossPercent,
        double? AverageInternetJitterMilliseconds,
        int InternetFailureCount,
        int MaximumInternetConsecutiveFailures)
    {
        public static NetworkAnalysisData Empty =>
            new(
                SampleCount: 0,

                AverageGatewayLatencyMilliseconds: null,
                MaximumGatewayLatencyMilliseconds: null,
                MaximumGatewayPacketLossPercent: null,
                AverageGatewayJitterMilliseconds: null,
                GatewayFailureCount: 0,
                MaximumGatewayConsecutiveFailures: 0,

                AverageInternetLatencyMilliseconds: null,
                MaximumInternetLatencyMilliseconds: null,
                MaximumInternetPacketLossPercent: null,
                AverageInternetJitterMilliseconds: null,
                InternetFailureCount: 0,
                MaximumInternetConsecutiveFailures: 0);
    }
    
    private static void AddNullableParameter(
        SqliteCommand command,
        string parameterName,
        object? value)
    {
        command.Parameters.AddWithValue(
            parameterName,
            value ?? DBNull.Value);
    }
    
    private static async Task<HardwareAnalysisData>
        ReadHardwareAnalysisAsync(
            SqliteConnection connection,
            Guid runId,
            CancellationToken cancellationToken)
    {
        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                COUNT(*) AS SampleCount,

                MAX(CpuName) AS CpuName,

                AVG(
                    CASE
                        WHEN CpuTemperatureCelsius > 0
                        THEN CpuTemperatureCelsius
                    END
                ) AS AverageCpuTemperatureCelsius,

                MAX(
                    CASE
                        WHEN CpuTemperatureCelsius > 0
                        THEN CpuTemperatureCelsius
                    END
                ) AS MaximumCpuTemperatureCelsius,

                MAX(
                    CASE
                        WHEN CpuMaximumCoreTemperatureCelsius > 0
                        THEN CpuMaximumCoreTemperatureCelsius
                    END
                ) AS MaximumCpuCoreTemperatureCelsius,

                AVG(
                    CASE
                        WHEN CpuAverageClockMegahertz > 0
                        THEN CpuAverageClockMegahertz
                    END
                ) AS AverageCpuClockMegahertz,

                MIN(
                    CASE
                        WHEN CpuAverageClockMegahertz > 0
                        THEN CpuAverageClockMegahertz
                    END
                ) AS MinimumCpuClockMegahertz,

                MAX(
                    CASE
                        WHEN CpuAverageClockMegahertz > 0
                        THEN CpuAverageClockMegahertz
                    END
                ) AS MaximumCpuClockMegahertz,

                AVG(
                    CASE
                        WHEN CpuPackagePowerWatts > 0
                        THEN CpuPackagePowerWatts
                    END
                ) AS AverageCpuPackagePowerWatts,

                MAX(
                    CASE
                        WHEN CpuPackagePowerWatts > 0
                        THEN CpuPackagePowerWatts
                    END
                ) AS MaximumCpuPackagePowerWatts,

                MAX(GpuName) AS GpuName,

                AVG(GpuLoadPercent)
                    AS AverageGpuLoadPercent,

                MAX(GpuLoadPercent)
                    AS MaximumGpuLoadPercent,

                AVG(
                    CASE
                        WHEN GpuTemperatureCelsius > 0
                        THEN GpuTemperatureCelsius
                    END
                ) AS AverageGpuTemperatureCelsius,

                MAX(
                    CASE
                        WHEN GpuTemperatureCelsius > 0
                        THEN GpuTemperatureCelsius
                    END
                ) AS MaximumGpuTemperatureCelsius,

                MAX(
                    CASE
                        WHEN GpuHotspotTemperatureCelsius > 0
                        THEN GpuHotspotTemperatureCelsius
                    END
                ) AS MaximumGpuHotspotTemperatureCelsius,

                AVG(
                    CASE
                        WHEN GpuCoreClockMegahertz > 0
                        THEN GpuCoreClockMegahertz
                    END
                ) AS AverageGpuCoreClockMegahertz,

                MIN(
                    CASE
                        WHEN GpuCoreClockMegahertz > 0
                        THEN GpuCoreClockMegahertz
                    END
                ) AS MinimumGpuCoreClockMegahertz,

                MAX(
                    CASE
                        WHEN GpuCoreClockMegahertz > 0
                        THEN GpuCoreClockMegahertz
                    END
                ) AS MaximumGpuCoreClockMegahertz,

                AVG(
                    CASE
                        WHEN GpuPowerWatts > 0
                        THEN GpuPowerWatts
                    END
                ) AS AverageGpuPowerWatts,

                MAX(
                    CASE
                        WHEN GpuPowerWatts > 0
                        THEN GpuPowerWatts
                    END
                ) AS MaximumGpuPowerWatts,

                MAX(GpuUsedMemoryMegabytes)
                    AS MaximumGpuUsedMemoryMegabytes,

                MAX(GpuTotalMemoryMegabytes)
                    AS GpuTotalMemoryMegabytes

            FROM HardwareMetricSamples
            WHERE RunId = $runId;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return HardwareAnalysisData.Empty;
        }

        return new HardwareAnalysisData(
            SampleCount:
                reader.GetInt64(
                    reader.GetOrdinal("SampleCount")),

            CpuName:
                ReadNullableString(
                    reader,
                    "CpuName"),

            AverageCpuTemperatureCelsius:
                ReadNullableDouble(
                    reader,
                    "AverageCpuTemperatureCelsius"),

            MaximumCpuTemperatureCelsius:
                ReadNullableDouble(
                    reader,
                    "MaximumCpuTemperatureCelsius"),

            MaximumCpuCoreTemperatureCelsius:
                ReadNullableDouble(
                    reader,
                    "MaximumCpuCoreTemperatureCelsius"),

            AverageCpuClockMegahertz:
                ReadNullableDouble(
                    reader,
                    "AverageCpuClockMegahertz"),

            MinimumCpuClockMegahertz:
                ReadNullableDouble(
                    reader,
                    "MinimumCpuClockMegahertz"),

            MaximumCpuClockMegahertz:
                ReadNullableDouble(
                    reader,
                    "MaximumCpuClockMegahertz"),

            AverageCpuPackagePowerWatts:
                ReadNullableDouble(
                    reader,
                    "AverageCpuPackagePowerWatts"),

            MaximumCpuPackagePowerWatts:
                ReadNullableDouble(
                    reader,
                    "MaximumCpuPackagePowerWatts"),

            GpuName:
                ReadNullableString(
                    reader,
                    "GpuName"),

            AverageGpuLoadPercent:
                ReadNullableDouble(
                    reader,
                    "AverageGpuLoadPercent"),

            MaximumGpuLoadPercent:
                ReadNullableDouble(
                    reader,
                    "MaximumGpuLoadPercent"),

            AverageGpuTemperatureCelsius:
                ReadNullableDouble(
                    reader,
                    "AverageGpuTemperatureCelsius"),

            MaximumGpuTemperatureCelsius:
                ReadNullableDouble(
                    reader,
                    "MaximumGpuTemperatureCelsius"),

            MaximumGpuHotspotTemperatureCelsius:
                ReadNullableDouble(
                    reader,
                    "MaximumGpuHotspotTemperatureCelsius"),

            AverageGpuCoreClockMegahertz:
                ReadNullableDouble(
                    reader,
                    "AverageGpuCoreClockMegahertz"),

            MinimumGpuCoreClockMegahertz:
                ReadNullableDouble(
                    reader,
                    "MinimumGpuCoreClockMegahertz"),

            MaximumGpuCoreClockMegahertz:
                ReadNullableDouble(
                    reader,
                    "MaximumGpuCoreClockMegahertz"),

            AverageGpuPowerWatts:
                ReadNullableDouble(
                    reader,
                    "AverageGpuPowerWatts"),

            MaximumGpuPowerWatts:
                ReadNullableDouble(
                    reader,
                    "MaximumGpuPowerWatts"),

            MaximumGpuUsedMemoryMegabytes:
                ReadNullableDouble(
                    reader,
                    "MaximumGpuUsedMemoryMegabytes"),

            GpuTotalMemoryMegabytes:
                ReadNullableDouble(
                    reader,
                    "GpuTotalMemoryMegabytes"));
    }
    
    private readonly record struct HardwareAnalysisData(
        long SampleCount,

        string? CpuName,
        double? AverageCpuTemperatureCelsius,
        double? MaximumCpuTemperatureCelsius,
        double? MaximumCpuCoreTemperatureCelsius,
        double? AverageCpuClockMegahertz,
        double? MinimumCpuClockMegahertz,
        double? MaximumCpuClockMegahertz,
        double? AverageCpuPackagePowerWatts,
        double? MaximumCpuPackagePowerWatts,

        string? GpuName,
        double? AverageGpuLoadPercent,
        double? MaximumGpuLoadPercent,
        double? AverageGpuTemperatureCelsius,
        double? MaximumGpuTemperatureCelsius,
        double? MaximumGpuHotspotTemperatureCelsius,
        double? AverageGpuCoreClockMegahertz,
        double? MinimumGpuCoreClockMegahertz,
        double? MaximumGpuCoreClockMegahertz,
        double? AverageGpuPowerWatts,
        double? MaximumGpuPowerWatts,
        double? MaximumGpuUsedMemoryMegabytes,
        double? GpuTotalMemoryMegabytes)
    {
        public static HardwareAnalysisData Empty =>
            new(
                SampleCount: 0,

                CpuName: null,
                AverageCpuTemperatureCelsius: null,
                MaximumCpuTemperatureCelsius: null,
                MaximumCpuCoreTemperatureCelsius: null,
                AverageCpuClockMegahertz: null,
                MinimumCpuClockMegahertz: null,
                MaximumCpuClockMegahertz: null,
                AverageCpuPackagePowerWatts: null,
                MaximumCpuPackagePowerWatts: null,

                GpuName: null,
                AverageGpuLoadPercent: null,
                MaximumGpuLoadPercent: null,
                AverageGpuTemperatureCelsius: null,
                MaximumGpuTemperatureCelsius: null,
                MaximumGpuHotspotTemperatureCelsius: null,
                AverageGpuCoreClockMegahertz: null,
                MinimumGpuCoreClockMegahertz: null,
                MaximumGpuCoreClockMegahertz: null,
                AverageGpuPowerWatts: null,
                MaximumGpuPowerWatts: null,
                MaximumGpuUsedMemoryMegabytes: null,
                GpuTotalMemoryMegabytes: null);
    }
    
    private async Task SaveDiskMetricsSampleAsync(
        Guid runId,
        DiskMetricsSample sample,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO DiskMetricSamples
            (
                RunId,
                Timestamp,

                DeviceId,
                DeviceName,

                TemperatureCelsius,

                ReadActivityPercent,
                WriteActivityPercent,
                TotalActivityPercent,

                ReadBytesPerSecond,
                WriteBytesPerSecond,

                UsedSpacePercent,
                FreeSpaceGigabytes,
                TotalSpaceGigabytes,

                RemainingLifePercent
            )
            VALUES
            (
                $runId,
                $timestamp,

                $deviceId,
                $deviceName,

                $temperatureCelsius,

                $readActivityPercent,
                $writeActivityPercent,
                $totalActivityPercent,

                $readBytesPerSecond,
                $writeBytesPerSecond,

                $usedSpacePercent,
                $freeSpaceGigabytes,
                $totalSpaceGigabytes,

                $remainingLifePercent
            );
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        command.Parameters.AddWithValue(
            "$timestamp",
            FormatDateTime(sample.Timestamp));

        command.Parameters.AddWithValue(
            "$deviceId",
            sample.DeviceId);

        command.Parameters.AddWithValue(
            "$deviceName",
            sample.DeviceName);

        AddNullableParameter(
            command,
            "$temperatureCelsius",
            sample.TemperatureCelsius);

        AddNullableParameter(
            command,
            "$readActivityPercent",
            sample.ReadActivityPercent);

        AddNullableParameter(
            command,
            "$writeActivityPercent",
            sample.WriteActivityPercent);

        AddNullableParameter(
            command,
            "$totalActivityPercent",
            sample.TotalActivityPercent);

        AddNullableParameter(
            command,
            "$readBytesPerSecond",
            sample.ReadBytesPerSecond);

        AddNullableParameter(
            command,
            "$writeBytesPerSecond",
            sample.WriteBytesPerSecond);

        AddNullableParameter(
            command,
            "$usedSpacePercent",
            sample.UsedSpacePercent);

        AddNullableParameter(
            command,
            "$freeSpaceGigabytes",
            sample.FreeSpaceGigabytes);

        AddNullableParameter(
            command,
            "$totalSpaceGigabytes",
            sample.TotalSpaceGigabytes);

        AddNullableParameter(
            command,
            "$remainingLifePercent",
            sample.RemainingLifePercent);

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }
    
    public async Task<IReadOnlyList<DiskRunAnalysis>>
        GetDiskRunAnalysesAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                DeviceId,
                MAX(DeviceName) AS DeviceName,
                COUNT(*) AS SampleCount,

                AVG(
                    CASE
                        WHEN TemperatureCelsius > 0
                        THEN TemperatureCelsius
                    END
                ) AS AverageTemperatureCelsius,

                MAX(
                    CASE
                        WHEN TemperatureCelsius > 0
                        THEN TemperatureCelsius
                    END
                ) AS MaximumTemperatureCelsius,

                AVG(ReadActivityPercent)
                    AS AverageReadActivityPercent,

                MAX(ReadActivityPercent)
                    AS MaximumReadActivityPercent,

                AVG(WriteActivityPercent)
                    AS AverageWriteActivityPercent,

                MAX(WriteActivityPercent)
                    AS MaximumWriteActivityPercent,

                AVG(TotalActivityPercent)
                    AS AverageTotalActivityPercent,

                MAX(TotalActivityPercent)
                    AS MaximumTotalActivityPercent,

                AVG(ReadBytesPerSecond)
                    AS AverageReadBytesPerSecond,

                MAX(ReadBytesPerSecond)
                    AS MaximumReadBytesPerSecond,

                AVG(WriteBytesPerSecond)
                    AS AverageWriteBytesPerSecond,

                MAX(WriteBytesPerSecond)
                    AS MaximumWriteBytesPerSecond,

                MAX(UsedSpacePercent)
                    AS MaximumUsedSpacePercent,

                MIN(FreeSpaceGigabytes)
                    AS MinimumFreeSpaceGigabytes,

                MAX(TotalSpaceGigabytes)
                    AS TotalSpaceGigabytes,

                MIN(RemainingLifePercent)
                    AS MinimumRemainingLifePercent

            FROM DiskMetricSamples
            WHERE RunId = $runId
            GROUP BY DeviceId
            ORDER BY DeviceName ASC;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        var analyses =
            new List<DiskRunAnalysis>();

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            analyses.Add(
                new DiskRunAnalysis(
                    RunId: runId,

                    DeviceId:
                        reader.GetString(
                            reader.GetOrdinal("DeviceId")),

                    DeviceName:
                        reader.GetString(
                            reader.GetOrdinal("DeviceName")),

                    SampleCount:
                        reader.GetInt64(
                            reader.GetOrdinal("SampleCount")),

                    AverageTemperatureCelsius:
                        ReadNullableDouble(
                            reader,
                            "AverageTemperatureCelsius"),

                    MaximumTemperatureCelsius:
                        ReadNullableDouble(
                            reader,
                            "MaximumTemperatureCelsius"),

                    AverageReadActivityPercent:
                        ReadNullableDouble(
                            reader,
                            "AverageReadActivityPercent"),

                    MaximumReadActivityPercent:
                        ReadNullableDouble(
                            reader,
                            "MaximumReadActivityPercent"),

                    AverageWriteActivityPercent:
                        ReadNullableDouble(
                            reader,
                            "AverageWriteActivityPercent"),

                    MaximumWriteActivityPercent:
                        ReadNullableDouble(
                            reader,
                            "MaximumWriteActivityPercent"),

                    AverageTotalActivityPercent:
                        ReadNullableDouble(
                            reader,
                            "AverageTotalActivityPercent"),

                    MaximumTotalActivityPercent:
                        ReadNullableDouble(
                            reader,
                            "MaximumTotalActivityPercent"),

                    AverageReadBytesPerSecond:
                        ReadNullableDouble(
                            reader,
                            "AverageReadBytesPerSecond"),

                    MaximumReadBytesPerSecond:
                        ReadNullableDouble(
                            reader,
                            "MaximumReadBytesPerSecond"),

                    AverageWriteBytesPerSecond:
                        ReadNullableDouble(
                            reader,
                            "AverageWriteBytesPerSecond"),

                    MaximumWriteBytesPerSecond:
                        ReadNullableDouble(
                            reader,
                            "MaximumWriteBytesPerSecond"),

                    MaximumUsedSpacePercent:
                        ReadNullableDouble(
                            reader,
                            "MaximumUsedSpacePercent"),

                    MinimumFreeSpaceGigabytes:
                        ReadNullableDouble(
                            reader,
                            "MinimumFreeSpaceGigabytes"),

                    TotalSpaceGigabytes:
                        ReadNullableDouble(
                            reader,
                            "TotalSpaceGigabytes"),

                    MinimumRemainingLifePercent:
                        ReadNullableDouble(
                            reader,
                            "MinimumRemainingLifePercent")));
        }

        return analyses;
    }
    
    public async Task<IReadOnlyList<ProcessRunAnalysis>>
        GetProcessRunAnalysesAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                ProcessName,

                MAX(ExecutablePath)
                    AS ExecutablePath,

                COUNT(*)
                    AS SampleCount,

                SUM(
                    CASE
                        WHEN IsForegroundProcess = 1
                        THEN 1
                        ELSE 0
                    END
                ) AS ForegroundSampleCount,

                AVG(CpuUsagePercent)
                    AS AverageCpuUsagePercent,

                MAX(CpuUsagePercent)
                    AS MaximumCpuUsagePercent,

                MAX(WorkingSetBytes)
                    AS MaximumWorkingSetBytes,

                MAX(PrivateMemoryBytes)
                    AS MaximumPrivateMemoryBytes,

                MAX(ThreadCount)
                    AS MaximumThreadCount,

                MAX(HandleCount)
                    AS MaximumHandleCount,

                AVG(ReadBytesPerSecond)
                    AS AverageReadBytesPerSecond,

                MAX(ReadBytesPerSecond)
                    AS MaximumReadBytesPerSecond,

                AVG(WriteBytesPerSecond)
                    AS AverageWriteBytesPerSecond,

                MAX(WriteBytesPerSecond)
                    AS MaximumWriteBytesPerSecond

            FROM ProcessMetricSamples
            WHERE RunId = $runId
            GROUP BY ProcessName
            ORDER BY
                ForegroundSampleCount DESC,
                AverageCpuUsagePercent DESC,
                MaximumWorkingSetBytes DESC;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        var analyses =
            new List<ProcessRunAnalysis>();

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            analyses.Add(
                new ProcessRunAnalysis(
                    RunId: runId,

                    ProcessName:
                        reader.GetString(
                            reader.GetOrdinal("ProcessName")),

                    ExecutablePath:
                        ReadNullableString(
                            reader,
                            "ExecutablePath"),

                    SampleCount:
                        reader.GetInt64(
                            reader.GetOrdinal("SampleCount")),

                    ForegroundSampleCount:
                        ReadInt64OrZero(
                            reader,
                            "ForegroundSampleCount"),

                    AverageCpuUsagePercent:
                        ReadDoubleOrZero(
                            reader,
                            "AverageCpuUsagePercent"),

                    MaximumCpuUsagePercent:
                        ReadDoubleOrZero(
                            reader,
                            "MaximumCpuUsagePercent"),

                    MaximumWorkingSetBytes:
                        ReadInt64OrZero(
                            reader,
                            "MaximumWorkingSetBytes"),

                    MaximumPrivateMemoryBytes:
                        ReadInt64OrZero(
                            reader,
                            "MaximumPrivateMemoryBytes"),

                    MaximumThreadCount:
                        ReadInt32OrZero(
                            reader,
                            "MaximumThreadCount"),

                    MaximumHandleCount:
                        ReadInt32OrZero(
                            reader,
                            "MaximumHandleCount"),

                    AverageReadBytesPerSecond:
                        ReadNullableDouble(
                            reader,
                            "AverageReadBytesPerSecond"),

                    MaximumReadBytesPerSecond:
                        ReadNullableDouble(
                            reader,
                            "MaximumReadBytesPerSecond"),

                    AverageWriteBytesPerSecond:
                        ReadNullableDouble(
                            reader,
                            "AverageWriteBytesPerSecond"),

                    MaximumWriteBytesPerSecond:
                        ReadNullableDouble(
                            reader,
                            "MaximumWriteBytesPerSecond")));
        }

        return analyses;
    }
    
    public async Task<IReadOnlyList<CpuCoreRunAnalysis>>
        GetCpuCoreRunAnalysesAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                CoreIndex,
                ThreadIndex,
                MAX(SensorName) AS SensorName,

                COUNT(*) AS SampleCount,

                AVG(LoadPercent)
                    AS AverageLoadPercent,

                MAX(LoadPercent)
                    AS MaximumLoadPercent,

                SUM(
                    CASE
                        WHEN LoadPercent >= 80
                        THEN 1
                        ELSE 0
                    END
                ) AS SamplesAbove80Percent,

                SUM(
                    CASE
                        WHEN LoadPercent >= 90
                        THEN 1
                        ELSE 0
                    END
                ) AS SamplesAbove90Percent,

                SUM(
                    CASE
                        WHEN LoadPercent >= 95
                        THEN 1
                        ELSE 0
                    END
                ) AS SamplesAbove95Percent

            FROM CpuCoreMetricSamples
            WHERE RunId = $runId

            GROUP BY
                CoreIndex,
                ThreadIndex

            ORDER BY
                MaximumLoadPercent DESC,
                AverageLoadPercent DESC,
                CoreIndex ASC,
                ThreadIndex ASC;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        var analyses =
            new List<CpuCoreRunAnalysis>();

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            analyses.Add(
                new CpuCoreRunAnalysis(
                    RunId: runId,

                    CoreIndex:
                        reader.GetInt32(
                            reader.GetOrdinal("CoreIndex")),

                    ThreadIndex:
                        reader.GetInt32(
                            reader.GetOrdinal("ThreadIndex")),

                    SensorName:
                        reader.GetString(
                            reader.GetOrdinal("SensorName")),

                    SampleCount:
                        reader.GetInt64(
                            reader.GetOrdinal("SampleCount")),

                    AverageLoadPercent:
                        ReadDoubleOrZero(
                            reader,
                            "AverageLoadPercent"),

                    MaximumLoadPercent:
                        ReadDoubleOrZero(
                            reader,
                            "MaximumLoadPercent"),

                    SamplesAbove80Percent:
                        ReadInt64OrZero(
                            reader,
                            "SamplesAbove80Percent"),

                    SamplesAbove90Percent:
                        ReadInt64OrZero(
                            reader,
                            "SamplesAbove90Percent"),

                    SamplesAbove95Percent:
                        ReadInt64OrZero(
                            reader,
                            "SamplesAbove95Percent")));
        }

        return analyses;
    }
    
    private async Task SaveProcessMetricsSampleAsync(
        Guid runId,
        ProcessMetricsSample sample,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO ProcessMetricSamples
            (
                RunId,
                Timestamp,

                ProcessId,
                ProcessName,
                ExecutablePath,
                IsForegroundProcess,

                CpuUsagePercent,

                WorkingSetBytes,
                PrivateMemoryBytes,

                ThreadCount,
                HandleCount,

                ReadBytesPerSecond,
                WriteBytesPerSecond
            )
            VALUES
            (
                $runId,
                $timestamp,

                $processId,
                $processName,
                $executablePath,
                $isForegroundProcess,

                $cpuUsagePercent,

                $workingSetBytes,
                $privateMemoryBytes,

                $threadCount,
                $handleCount,

                $readBytesPerSecond,
                $writeBytesPerSecond
            );
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        command.Parameters.AddWithValue(
            "$timestamp",
            FormatDateTime(sample.Timestamp));

        command.Parameters.AddWithValue(
            "$processId",
            sample.ProcessId);

        command.Parameters.AddWithValue(
            "$processName",
            sample.ProcessName);

        AddNullableParameter(
            command,
            "$executablePath",
            sample.ExecutablePath);

        command.Parameters.AddWithValue(
            "$isForegroundProcess",
            sample.IsForegroundProcess ? 1 : 0);

        command.Parameters.AddWithValue(
            "$cpuUsagePercent",
            sample.CpuUsagePercent);

        command.Parameters.AddWithValue(
            "$workingSetBytes",
            sample.WorkingSetBytes);

        command.Parameters.AddWithValue(
            "$privateMemoryBytes",
            sample.PrivateMemoryBytes);

        command.Parameters.AddWithValue(
            "$threadCount",
            sample.ThreadCount);

        command.Parameters.AddWithValue(
            "$handleCount",
            sample.HandleCount);

        AddNullableParameter(
            command,
            "$readBytesPerSecond",
            sample.ReadBytesPerSecond);

        AddNullableParameter(
            command,
            "$writeBytesPerSecond",
            sample.WriteBytesPerSecond);

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }
    
    private async Task SaveCpuCoreMetricsSampleAsync(
        Guid runId,
        CpuCoreMetricsSample sample,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO CpuCoreMetricSamples
            (
                RunId,
                Timestamp,
                CoreIndex,
                ThreadIndex,
                SensorName,
                LoadPercent
            )
            VALUES
            (
                $runId,
                $timestamp,
                $coreIndex,
                $threadIndex,
                $sensorName,
                $loadPercent
            );
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        command.Parameters.AddWithValue(
            "$timestamp",
            FormatDateTime(sample.Timestamp));

        command.Parameters.AddWithValue(
            "$coreIndex",
            sample.CoreIndex);

        command.Parameters.AddWithValue(
            "$threadIndex",
            sample.ThreadIndex);

        command.Parameters.AddWithValue(
            "$sensorName",
            sample.SensorName);

        command.Parameters.AddWithValue(
            "$loadPercent",
            sample.LoadPercent);

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }
    
    private async Task SaveFrameMetricsSampleAsync(
        Guid runId,
        FrameMetricsSample sample,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO FrameMetricSamples
            (
                RunId, Timestamp, Application, ProcessId,
                PresentRuntime, PresentMode,
                FrameTimeMilliseconds, FramesPerSecond,
                CpuBusyMilliseconds, GpuTimeMilliseconds,
                DisplayLatencyMilliseconds
            )
            VALUES
            (
                $runId, $timestamp, $application, $processId,
                $presentRuntime, $presentMode,
                $frameTimeMilliseconds, $framesPerSecond,
                $cpuBusyMilliseconds, $gpuTimeMilliseconds,
                $displayLatencyMilliseconds
            );
            """;

        command.Parameters.AddWithValue("$runId", runId.ToString());
        command.Parameters.AddWithValue("$timestamp", FormatDateTime(sample.Timestamp));
        command.Parameters.AddWithValue("$application", sample.Application);
        command.Parameters.AddWithValue("$processId", sample.ProcessId);
        AddNullableParameter(command, "$presentRuntime", sample.PresentRuntime);
        AddNullableParameter(command, "$presentMode", sample.PresentMode);
        command.Parameters.AddWithValue("$frameTimeMilliseconds", sample.FrameTimeMilliseconds);
        command.Parameters.AddWithValue("$framesPerSecond", sample.FramesPerSecond);
        AddNullableParameter(command, "$cpuBusyMilliseconds", sample.CpuBusyMilliseconds);
        AddNullableParameter(command, "$gpuTimeMilliseconds", sample.GpuTimeMilliseconds);
        AddNullableParameter(command, "$displayLatencyMilliseconds", sample.DisplayLatencyMilliseconds);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveSamplesAsync(
        Guid runId,
        IReadOnlyList<IMetricSample> samples,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Count == 0)
        {
            return;
        }

        foreach (IMetricSample sample in samples)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await SaveSampleAsync(
                runId,
                sample,
                cancellationToken);
        }
    }
    
    public async Task<RunSampleCounts?>
        GetRunSampleCountsAsync(
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
                r.Id,

                (
                    SELECT COUNT(*)
                    FROM SystemMetricSamples samples
                    WHERE samples.RunId = r.Id
                ) AS SystemSamples,

                (
                    SELECT COUNT(*)
                    FROM NetworkMetricSamples samples
                    WHERE samples.RunId = r.Id
                ) AS NetworkSamples,

                (
                    SELECT COUNT(*)
                    FROM HardwareMetricSamples samples
                    WHERE samples.RunId = r.Id
                ) AS HardwareSamples,

                (
                    SELECT COUNT(*)
                    FROM CpuCoreMetricSamples samples
                    WHERE samples.RunId = r.Id
                ) AS CpuCoreSamples,

                (
                    SELECT COUNT(*)
                    FROM DiskMetricSamples samples
                    WHERE samples.RunId = r.Id
                ) AS DiskSamples,

                (
                    SELECT COUNT(*)
                    FROM ProcessMetricSamples samples
                    WHERE samples.RunId = r.Id
                ) AS ProcessSamples,

                (
                    SELECT COUNT(*)
                    FROM NetworkIncidents incidents
                    WHERE incidents.RunId = r.Id
                ) AS NetworkIncidents

            FROM Runs r
            WHERE r.Id = $runId;
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

        return new RunSampleCounts(
            RunId: runId,

            SystemSamples:
                ReadInt64OrZero(
                    reader,
                    "SystemSamples"),

            NetworkSamples:
                ReadInt64OrZero(
                    reader,
                    "NetworkSamples"),

            HardwareSamples:
                ReadInt64OrZero(
                    reader,
                    "HardwareSamples"),

            CpuCoreSamples:
                ReadInt64OrZero(
                    reader,
                    "CpuCoreSamples"),

            DiskSamples:
                ReadInt64OrZero(
                    reader,
                    "DiskSamples"),

            ProcessSamples:
                ReadInt64OrZero(
                    reader,
                    "ProcessSamples"),

            NetworkIncidents:
                ReadInt64OrZero(
                    reader,
                    "NetworkIncidents"));
    }
    
    public async Task<IReadOnlyList<FrameRunAnalysis>>
        GetFrameRunAnalysesAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            WITH RankedFrames AS
            (
                SELECT
                    Application,
                    ProcessId,
                    Timestamp,
                    FrameTimeMilliseconds,
                    FramesPerSecond,

                    ROW_NUMBER() OVER
                    (
                        PARTITION BY Application, ProcessId
                        ORDER BY FrameTimeMilliseconds ASC
                    ) AS RowNumberAscending,

                    COUNT(*) OVER
                    (
                        PARTITION BY Application, ProcessId
                    ) AS PartitionCount

                FROM FrameMetricSamples
                WHERE RunId = $runId
            ),
            FrameAggregates AS
            (
                SELECT
                    Application,
                    ProcessId,
                    COUNT(*) AS FrameCount,
                    MIN(Timestamp) AS FirstFrameAt,
                    MAX(Timestamp) AS LastFrameAt,
                    AVG(FramesPerSecond) AS AverageFramesPerSecond,
                    MIN(FramesPerSecond) AS MinimumFramesPerSecond,
                    MAX(FramesPerSecond) AS MaximumFramesPerSecond,

                    1000.0 / AVG(
                        CASE
                            WHEN RowNumberAscending >=
                                 MAX(1, CAST(PartitionCount * 0.99 AS INTEGER))
                            THEN FrameTimeMilliseconds
                        END
                    ) AS OnePercentLowFramesPerSecond,

                    1000.0 / AVG(
                        CASE
                            WHEN RowNumberAscending >=
                                 MAX(1, CAST(PartitionCount * 0.999 AS INTEGER))
                            THEN FrameTimeMilliseconds
                        END
                    ) AS PointOnePercentLowFramesPerSecond,

                    AVG(FrameTimeMilliseconds)
                        AS AverageFrameTimeMilliseconds,

                    MIN(
                        CASE
                            WHEN RowNumberAscending >=
                                 MAX(1, CAST(PartitionCount * 0.95 AS INTEGER))
                            THEN FrameTimeMilliseconds
                        END
                    ) AS P95FrameTimeMilliseconds,

                    MIN(
                        CASE
                            WHEN RowNumberAscending >=
                                 MAX(1, CAST(PartitionCount * 0.99 AS INTEGER))
                            THEN FrameTimeMilliseconds
                        END
                    ) AS P99FrameTimeMilliseconds,

                    MIN(
                        CASE
                            WHEN RowNumberAscending >=
                                 MAX(1, CAST(PartitionCount * 0.999 AS INTEGER))
                            THEN FrameTimeMilliseconds
                        END
                    ) AS P999FrameTimeMilliseconds,

                    MAX(FrameTimeMilliseconds)
                        AS MaximumFrameTimeMilliseconds,

                    SUM(CASE WHEN FrameTimeMilliseconds >= 25 THEN 1 ELSE 0 END)
                        AS FrameSpikesAbove25Milliseconds,

                    SUM(CASE WHEN FrameTimeMilliseconds >= 50 THEN 1 ELSE 0 END)
                        AS FrameSpikesAbove50Milliseconds,

                    SUM(CASE WHEN FrameTimeMilliseconds >= 100 THEN 1 ELSE 0 END)
                        AS FrameSpikesAbove100Milliseconds

                FROM RankedFrames
                GROUP BY Application, ProcessId
                HAVING COUNT(*) >= 30
            ),
            ProcessAggregates AS
            (
                SELECT
                    ProcessId,
                    MAX(ProcessName) AS ProcessName,
                    MAX(ExecutablePath) AS ExecutablePath,
                    COUNT(*) AS ProcessSampleCount,

                    SUM(
                        CASE
                            WHEN IsForegroundProcess = 1
                            THEN 1
                            ELSE 0
                        END
                    ) AS ForegroundSampleCount,

                    100.0 * SUM(
                        CASE
                            WHEN IsForegroundProcess = 1
                            THEN 1
                            ELSE 0
                        END
                    ) / COUNT(*) AS ForegroundSharePercent,

                    AVG(CpuUsagePercent)
                        AS AverageProcessCpuPercent,

                    MAX(CpuUsagePercent)
                        AS MaximumProcessCpuPercent

                FROM ProcessMetricSamples
                WHERE RunId = $runId
                GROUP BY ProcessId
            )
            SELECT
                frames.*,

                COALESCE(processes.ProcessName, '')
                    AS CandidateProcessName,

                processes.ExecutablePath
                    AS CandidateExecutablePath,

                COALESCE(processes.ProcessSampleCount, 0)
                    AS ProcessSampleCount,

                COALESCE(processes.ForegroundSampleCount, 0)
                    AS ForegroundSampleCount,

                COALESCE(processes.ForegroundSharePercent, 0)
                    AS ForegroundSharePercent,

                COALESCE(processes.AverageProcessCpuPercent, 0)
                    AS AverageProcessCpuPercent,

                COALESCE(processes.MaximumProcessCpuPercent, 0)
                    AS MaximumProcessCpuPercent,

                MAX(
                    0,
                    (julianday(frames.LastFrameAt) -
                     julianday(frames.FirstFrameAt)) * 86400.0
                ) AS CaptureDurationSeconds

            FROM FrameAggregates frames
            LEFT JOIN ProcessAggregates processes
                ON processes.ProcessId = frames.ProcessId
            ORDER BY frames.FrameCount DESC;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        var candidates =
            new List<FrameAnalysisCandidate>();

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            candidates.Add(
                new FrameAnalysisCandidate(
                    Analysis:
                        new FrameRunAnalysis(
                            RunId: runId,
                            Application:
                                reader.GetString(
                                    reader.GetOrdinal("Application")),
                            ProcessId:
                                reader.GetInt32(
                                    reader.GetOrdinal("ProcessId")),
                            FrameCount:
                                ReadInt64OrZero(reader, "FrameCount"),
                            FirstFrameAt:
                                ParseDateTime(
                                    reader.GetString(
                                        reader.GetOrdinal("FirstFrameAt"))),
                            LastFrameAt:
                                ParseDateTime(
                                    reader.GetString(
                                        reader.GetOrdinal("LastFrameAt"))),
                            AverageFramesPerSecond:
                                ReadDoubleOrZero(
                                    reader,
                                    "AverageFramesPerSecond"),
                            MinimumFramesPerSecond:
                                ReadDoubleOrZero(
                                    reader,
                                    "MinimumFramesPerSecond"),
                            MaximumFramesPerSecond:
                                ReadDoubleOrZero(
                                    reader,
                                    "MaximumFramesPerSecond"),
                            OnePercentLowFramesPerSecond:
                                ReadDoubleOrZero(
                                    reader,
                                    "OnePercentLowFramesPerSecond"),
                            PointOnePercentLowFramesPerSecond:
                                ReadDoubleOrZero(
                                    reader,
                                    "PointOnePercentLowFramesPerSecond"),
                            AverageFrameTimeMilliseconds:
                                ReadDoubleOrZero(
                                    reader,
                                    "AverageFrameTimeMilliseconds"),
                            P95FrameTimeMilliseconds:
                                ReadDoubleOrZero(
                                    reader,
                                    "P95FrameTimeMilliseconds"),
                            P99FrameTimeMilliseconds:
                                ReadDoubleOrZero(
                                    reader,
                                    "P99FrameTimeMilliseconds"),
                            P999FrameTimeMilliseconds:
                                ReadDoubleOrZero(
                                    reader,
                                    "P999FrameTimeMilliseconds"),
                            MaximumFrameTimeMilliseconds:
                                ReadDoubleOrZero(
                                    reader,
                                    "MaximumFrameTimeMilliseconds"),
                            FrameSpikesAbove25Milliseconds:
                                ReadInt64OrZero(
                                    reader,
                                    "FrameSpikesAbove25Milliseconds"),
                            FrameSpikesAbove50Milliseconds:
                                ReadInt64OrZero(
                                    reader,
                                    "FrameSpikesAbove50Milliseconds"),
                            FrameSpikesAbove100Milliseconds:
                                ReadInt64OrZero(
                                    reader,
                                    "FrameSpikesAbove100Milliseconds"),
                            ProcessSampleCount:
                                ReadInt64OrZero(
                                    reader,
                                    "ProcessSampleCount"),
                            ForegroundSampleCount:
                                ReadInt64OrZero(
                                    reader,
                                    "ForegroundSampleCount"),
                            ForegroundSharePercent:
                                ReadDoubleOrZero(
                                    reader,
                                    "ForegroundSharePercent"),
                            AverageProcessCpuPercent:
                                ReadDoubleOrZero(
                                    reader,
                                    "AverageProcessCpuPercent"),
                            MaximumProcessCpuPercent:
                                ReadDoubleOrZero(
                                    reader,
                                    "MaximumProcessCpuPercent"),
                            CaptureDurationSeconds:
                                ReadDoubleOrZero(
                                    reader,
                                    "CaptureDurationSeconds"),
                            SelectionScore: 0,
                            IsLikelyGame: false,
                            MatchedToProcessId: null,
                            MatchedToProcessName: null,
                            MatchReason:
                                FrameProcessMatchReason.NotSelected),

                    CandidateProcessName:
                        reader.GetString(
                            reader.GetOrdinal("CandidateProcessName")),

                    CandidateExecutablePath:
                        ReadNullableString(
                            reader,
                            "CandidateExecutablePath")));
        }

        if (candidates.Count == 0)
        {
            return [];
        }

        ProcessIdentity? mainApplication =
            await ReadMainApplicationIdentityAsync(
                connection,
                runId,
                cancellationToken);

        FrameAnalysisCandidate selectedCandidate =
            SelectFrameCandidate(
                candidates,
                mainApplication);

        var analyses =
            new List<FrameRunAnalysis>(
                candidates.Count);

        foreach (FrameAnalysisCandidate candidate
                 in candidates)
        {
            bool isSelected =
                candidate.Analysis.ProcessId ==
                selectedCandidate.Analysis.ProcessId;

            FrameCandidateMatch match =
                EvaluateFrameCandidate(
                    candidate,
                    mainApplication);

            analyses.Add(
                candidate.Analysis with
                {
                    SelectionScore =
                        match.Score,

                    IsLikelyGame =
                        isSelected,

                    MatchedToProcessId =
                        isSelected
                            ? mainApplication?.ProcessId
                            : null,

                    MatchedToProcessName =
                        isSelected
                            ? mainApplication?.ProcessName
                            : null,

                    MatchReason =
                        isSelected
                            ? match.Reason
                            : FrameProcessMatchReason.NotSelected
                });
        }

        return analyses
            .OrderByDescending(analysis =>
                analysis.IsLikelyGame)
            .ThenByDescending(analysis =>
                analysis.SelectionScore)
            .ThenByDescending(analysis =>
                analysis.FrameCount)
            .ToArray();
    }

    private static async Task<ProcessIdentity?>
        ReadMainApplicationIdentityAsync(
            SqliteConnection connection,
            Guid runId,
            CancellationToken cancellationToken)
    {
        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                ProcessId,
                ProcessName,
                MAX(ExecutablePath) AS ExecutablePath,
                COUNT(*) AS SampleCount,

                SUM(
                    CASE
                        WHEN IsForegroundProcess = 1
                        THEN 1
                        ELSE 0
                    END
                ) AS ForegroundSampleCount,

                AVG(CpuUsagePercent)
                    AS AverageCpuUsagePercent

            FROM ProcessMetricSamples
            WHERE RunId = $runId
            GROUP BY ProcessId, ProcessName
            ORDER BY
                ForegroundSampleCount DESC,
                AverageCpuUsagePercent DESC,
                SampleCount DESC
            LIMIT 1;
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

        return new ProcessIdentity(
            ProcessId:
                reader.GetInt32(
                    reader.GetOrdinal("ProcessId")),

            ProcessName:
                reader.GetString(
                    reader.GetOrdinal("ProcessName")),

            ExecutablePath:
                ReadNullableString(
                    reader,
                    "ExecutablePath"));
    }

    private static FrameAnalysisCandidate
        SelectFrameCandidate(
            IReadOnlyList<FrameAnalysisCandidate> candidates,
            ProcessIdentity? mainApplication)
    {
        return candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Match = EvaluateFrameCandidate(
                    candidate,
                    mainApplication)
            })
            .OrderByDescending(item =>
                item.Match.Score)
            .ThenByDescending(item =>
                item.Candidate.Analysis.FrameCount)
            .ThenByDescending(item =>
                item.Candidate.Analysis.CaptureDurationSeconds)
            .First()
            .Candidate;
    }

    private static FrameCandidateMatch
        EvaluateFrameCandidate(
            FrameAnalysisCandidate candidate,
            ProcessIdentity? mainApplication)
    {
        if (mainApplication is null)
        {
            return new FrameCandidateMatch(
                Score:
                    CreateFallbackFrameScore(
                        candidate.Analysis),

                Reason:
                    FrameProcessMatchReason.FallbackScore);
        }

        if (candidate.Analysis.ProcessId ==
            mainApplication.ProcessId)
        {
            return new FrameCandidateMatch(
                Score: 10_000,
                Reason:
                    FrameProcessMatchReason.ExactProcessId);
        }

        string frameName =
            NormalizeProcessName(
                candidate.Analysis.Application);

        string candidateName =
            NormalizeProcessName(
                candidate.CandidateProcessName);

        string mainName =
            NormalizeProcessName(
                mainApplication.ProcessName);

        if (string.Equals(
                frameName,
                mainName,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                candidateName,
                mainName,
                StringComparison.OrdinalIgnoreCase))
        {
            return new FrameCandidateMatch(
                Score: 9_000,
                Reason:
                    FrameProcessMatchReason.ExactProcessName);
        }

        int commonPathDepth =
            GetCommonDirectoryDepth(
                candidate.CandidateExecutablePath,
                mainApplication.ExecutablePath);

        if (commonPathDepth >= 4)
        {
            return new FrameCandidateMatch(
                Score:
                    8_000 +
                    commonPathDepth * 10 +
                    CreateFallbackFrameScore(
                        candidate.Analysis),

                Reason:
                    FrameProcessMatchReason.SharedInstallationDirectory);
        }

        if (commonPathDepth >= 2)
        {
            return new FrameCandidateMatch(
                Score:
                    7_000 +
                    commonPathDepth * 10 +
                    CreateFallbackFrameScore(
                        candidate.Analysis),

                Reason:
                    FrameProcessMatchReason.SharedPublisherDirectory);
        }

        return new FrameCandidateMatch(
            Score:
                CreateFallbackFrameScore(
                    candidate.Analysis),

            Reason:
                FrameProcessMatchReason.FallbackScore);
    }

    private static double CreateFallbackFrameScore(
        FrameRunAnalysis analysis)
    {
        return
            Math.Min(
                analysis.FrameCount / 500.0,
                20) +
            Math.Min(
                analysis.CaptureDurationSeconds / 6.0,
                20) +
            Math.Min(
                analysis.ForegroundSharePercent,
                100) *
            0.50 +
            Math.Min(
                analysis.AverageProcessCpuPercent,
                100) *
            0.20;
    }

    private static string NormalizeProcessName(
        string? processName)
    {
        if (string.IsNullOrWhiteSpace(
                processName))
        {
            return string.Empty;
        }

        return Path.GetFileNameWithoutExtension(
                processName.Trim())
            .Replace(
                "-",
                string.Empty,
                StringComparison.Ordinal)
            .Replace(
                "_",
                string.Empty,
                StringComparison.Ordinal)
            .Replace(
                " ",
                string.Empty,
                StringComparison.Ordinal)
            .ToUpperInvariant();
    }

    private static int GetCommonDirectoryDepth(
        string? firstExecutablePath,
        string? secondExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(
                firstExecutablePath) ||
            string.IsNullOrWhiteSpace(
                secondExecutablePath))
        {
            return 0;
        }

        string? firstDirectory =
            Path.GetDirectoryName(
                firstExecutablePath);

        string? secondDirectory =
            Path.GetDirectoryName(
                secondExecutablePath);

        if (string.IsNullOrWhiteSpace(
                firstDirectory) ||
            string.IsNullOrWhiteSpace(
                secondDirectory))
        {
            return 0;
        }

        string[] firstParts =
            firstDirectory.Split(
                Path.DirectorySeparatorChar,
                StringSplitOptions.RemoveEmptyEntries);

        string[] secondParts =
            secondDirectory.Split(
                Path.DirectorySeparatorChar,
                StringSplitOptions.RemoveEmptyEntries);

        int maximum =
            Math.Min(
                firstParts.Length,
                secondParts.Length);

        int commonDepth = 0;

        for (int index = 0;
             index < maximum;
             index++)
        {
            if (!string.Equals(
                    firstParts[index],
                    secondParts[index],
                    StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            commonDepth++;
        }

        return commonDepth;
    }

    private sealed record FrameAnalysisCandidate(
        FrameRunAnalysis Analysis,
        string CandidateProcessName,
        string? CandidateExecutablePath);

    private sealed record ProcessIdentity(
        int ProcessId,
        string ProcessName,
        string? ExecutablePath);

    private readonly record struct FrameCandidateMatch(
        double Score,
        FrameProcessMatchReason Reason);

    public async Task<IReadOnlyList<RunTimelineEvent>>
        GetRunTimelineEventsAsync(
            Guid runId,
            int maximumEventCount = 500,
            CancellationToken cancellationToken = default)
    {
        if (maximumEventCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumEventCount),
                "Die maximale Ereignisanzahl muss größer als null sein.");
        }

        IReadOnlyList<FrameRunAnalysis> frameAnalyses =
            await GetFrameRunAnalysesAsync(
                runId,
                cancellationToken);

        int likelyFrameProcessId =
            frameAnalyses
                .FirstOrDefault(analysis =>
                    analysis.IsLikelyGame)
                ?.ProcessId
            ?? -1;

        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                Timestamp,
                Severity,
                Category,
                EventCode,
                SourceId,
                SourceName,
                Title,
                Description,
                NumericValue,
                Unit,
                IsCorrelation
            FROM
            (
                SELECT
                    Timestamp,
                    2 AS Severity,
                    1 AS Category,
                    'CPU_THREAD_HIGH' AS EventCode,
                    'CPU:CORE:' || CoreIndex || ':THREAD:' || ThreadIndex AS SourceId,
                    'Core ' || CoreIndex || ', Thread ' || ThreadIndex AS SourceName,
                    'CPU-Thread sehr hoch ausgelastet' AS Title,
                    SensorName || ' war zu mindestens 95 % ausgelastet.' AS Description,
                    LoadPercent AS NumericValue,
                    '%' AS Unit,
                    0 AS IsCorrelation
                FROM CpuCoreMetricSamples
                WHERE RunId = $runId
                  AND LoadPercent >= 95

                UNION ALL

                SELECT
                    Timestamp,
                    CASE WHEN MemoryUsagePercent >= 95 THEN 3 ELSE 2 END,
                    3,
                    'MEMORY_USAGE_HIGH',
                    'SYSTEM:MEMORY',
                    'Arbeitsspeicher',
                    CASE
                        WHEN MemoryUsagePercent >= 95
                        THEN 'Arbeitsspeicher nahezu vollständig belegt'
                        ELSE 'Hohe Arbeitsspeicherauslastung'
                    END,
                    'Die gesamte RAM-Auslastung überschritt den Grenzwert.',
                    MemoryUsagePercent,
                    '%',
                    0
                FROM SystemMetricSamples
                WHERE RunId = $runId
                  AND MemoryUsagePercent >= 90

                UNION ALL

                SELECT
                    Timestamp,
                    1,
                    2,
                    'GPU_LOAD_HIGH',
                    'GPU:PRIMARY',
                    COALESCE(GpuName, 'Primäre GPU'),
                    'GPU erreichte nahezu Volllast',
                    'Die GPU-Auslastung lag bei mindestens 98 %.',
                    GpuLoadPercent,
                    '%',
                    0
                FROM HardwareMetricSamples
                WHERE RunId = $runId
                  AND GpuLoadPercent >= 98

                UNION ALL

                SELECT
                    Timestamp,
                    CASE
                        WHEN CpuMaximumCoreTemperatureCelsius >= 95
                          OR CpuTemperatureCelsius >= 95
                        THEN 3 ELSE 2
                    END,
                    1,
                    'CPU_TEMPERATURE_HIGH',
                    'CPU:TEMPERATURE',
                    COALESCE(CpuName, 'CPU'),
                    CASE
                        WHEN CpuMaximumCoreTemperatureCelsius >= 95
                          OR CpuTemperatureCelsius >= 95
                        THEN 'CPU-Temperatur kritisch hoch'
                        ELSE 'CPU-Temperatur sehr hoch'
                    END,
                    'Die CPU-Temperatur überschritt den Grenzwert.',
                    MAX(
                        COALESCE(CpuTemperatureCelsius, 0),
                        COALESCE(CpuMaximumCoreTemperatureCelsius, 0)
                    ),
                    '°C',
                    0
                FROM HardwareMetricSamples
                WHERE RunId = $runId
                  AND (
                      CpuTemperatureCelsius >= 90
                      OR CpuMaximumCoreTemperatureCelsius >= 90
                  )

                UNION ALL

                SELECT
                    Timestamp,
                    CASE WHEN GpuTemperatureCelsius >= 90 THEN 3 ELSE 2 END,
                    2,
                    'GPU_TEMPERATURE_HIGH',
                    'GPU:TEMPERATURE',
                    COALESCE(GpuName, 'Primäre GPU'),
                    'GPU-Temperatur sehr hoch',
                    'Die GPU-Temperatur überschritt den Grenzwert.',
                    GpuTemperatureCelsius,
                    '°C',
                    0
                FROM HardwareMetricSamples
                WHERE RunId = $runId
                  AND GpuTemperatureCelsius >= 85

                UNION ALL

                SELECT
                    Timestamp,
                    2,
                    4,
                    'DISK_ACTIVITY_HIGH',
                    'DISK:' || DeviceId,
                    DeviceName,
                    'Datenträger nahezu vollständig ausgelastet',
                    DeviceName || ' erreichte mindestens 95 % Aktivität.',
                    TotalActivityPercent,
                    '%',
                    0
                FROM DiskMetricSamples
                WHERE RunId = $runId
                  AND TotalActivityPercent >= 95

                UNION ALL

                SELECT
                    Timestamp,
                    3,
                    5,
                    'NETWORK_GATEWAY_FAILURE',
                    'NETWORK:GATEWAY',
                    COALESCE(GatewayAddress, 'Standardgateway'),
                    'Standardgateway nicht erreichbar',
                    'Die Verbindung zum lokalen Standardgateway ist fehlgeschlagen.',
                    GatewayConsecutiveFailures,
                    'Fehler',
                    0
                FROM NetworkMetricSamples
                WHERE RunId = $runId
                  AND GatewaySuccess = 0

                UNION ALL

                SELECT
                    Timestamp,
                    2,
                    5,
                    'NETWORK_INTERNET_FAILURE',
                    'NETWORK:INTERNET',
                    InternetTarget,
                    'Internetziel nicht erreichbar',
                    'Die Internetprüfung ist fehlgeschlagen, während das Gateway erreichbar blieb.',
                    InternetConsecutiveFailures,
                    'Fehler',
                    0
                FROM NetworkMetricSamples
                WHERE RunId = $runId
                  AND InternetSuccess = 0
                  AND GatewaySuccess = 1

                UNION ALL

                SELECT
                    Timestamp,
                    CASE
                        WHEN IsForegroundProcess = 0 AND CpuUsagePercent >= 40 THEN 2
                        ELSE 1
                    END,
                    6,
                    'PROCESS_CPU_HIGH',
                    'PROCESS:' || ProcessId || ':CPU',
                    ProcessName,
                    CASE
                        WHEN IsForegroundProcess = 0
                        THEN 'Hohe CPU-Last durch Hintergrundprozess'
                        ELSE 'Hohe CPU-Last des Vordergrundprozesses'
                    END,
                    ProcessName || ' erreichte eine hohe CPU-Auslastung.',
                    CpuUsagePercent,
                    '%',
                    0
                FROM ProcessMetricSamples
                WHERE RunId = $runId
                  AND CpuUsagePercent >= 25

                UNION ALL

                SELECT
                    Timestamp,
                    1,
                    6,
                    'PROCESS_READ_HIGH',
                    'PROCESS:' || ProcessId || ':READ',
                    ProcessName,
                    'Hohe Leseaktivität eines Prozesses',
                    ProcessName || ' las mit hoher Geschwindigkeit vom Datenträger.',
                    ReadBytesPerSecond / 1048576.0,
                    'MB/s',
                    0
                FROM ProcessMetricSamples
                WHERE RunId = $runId
                  AND ReadBytesPerSecond >= 104857600

                UNION ALL

                SELECT
                    Timestamp,
                    CASE WHEN IsForegroundProcess = 0 THEN 2 ELSE 1 END,
                    6,
                    'PROCESS_WRITE_HIGH',
                    'PROCESS:' || ProcessId || ':WRITE',
                    ProcessName,
                    'Hohe Schreibaktivität eines Prozesses',
                    ProcessName || ' schrieb mit hoher Geschwindigkeit auf den Datenträger.',
                    WriteBytesPerSecond / 1048576.0,
                    'MB/s',
                    0
                FROM ProcessMetricSamples
                WHERE RunId = $runId
                  AND WriteBytesPerSecond >= 52428800

                UNION ALL

                SELECT
                    Timestamp,
                    CASE WHEN WorkingSetBytes >= 8589934592 THEN 2 ELSE 1 END,
                    6,
                    'PROCESS_MEMORY_HIGH',
                    'PROCESS:' || ProcessId || ':MEMORY',
                    ProcessName,
                    'Hoher Arbeitsspeicherverbrauch eines Prozesses',
                    ProcessName || ' belegte viel Arbeitsspeicher.',
                    WorkingSetBytes / 1073741824.0,
                    'GB',
                    0
                FROM ProcessMetricSamples
                WHERE RunId = $runId
                  AND WorkingSetBytes >= 4294967296

                UNION ALL

                SELECT
                    Timestamp,
                    1,
                    6,
                    'PROCESS_FOREGROUND_CHANGED',
                    'PROCESS:FOREGROUND:' || ProcessId,
                    ProcessName,
                    'Vordergrundprozess gewechselt',
                    ProcessName || ' wurde als neuer Vordergrundprozess erkannt.',
                    NULL,
                    NULL,
                    0
                FROM
                (
                    SELECT
                        Timestamp,
                        ProcessId,
                        ProcessName,
                        LAG(ProcessId) OVER (ORDER BY Timestamp) AS PreviousProcessId
                    FROM ProcessMetricSamples
                    WHERE RunId = $runId
                      AND IsForegroundProcess = 1
                ) foregroundChanges
                WHERE PreviousProcessId IS NULL
                   OR PreviousProcessId <> ProcessId

                UNION ALL

                SELECT
                    Timestamp,
                    CASE WHEN FrameTimeMilliseconds >= 100 THEN 3 ELSE 2 END,
                    8,
                    'FRAME_TIME_SPIKE',
                    'FRAME:' || ProcessId,
                    Application,
                    CASE
                        WHEN FrameTimeMilliseconds >= 100 THEN 'Sehr starker Frametime-Spike'
                        WHEN FrameTimeMilliseconds >= 50 THEN 'Starker Frametime-Spike'
                        ELSE 'Frametime-Spike'
                    END,
                    Application || ' benötigte ' || ROUND(FrameTimeMilliseconds, 1) || ' ms für einen Frame.',
                    FrameTimeMilliseconds,
                    'ms',
                    0
                FROM FrameMetricSamples
                WHERE RunId = $runId
                  AND ProcessId = $frameProcessId
                  AND FrameTimeMilliseconds >= 25

                UNION ALL

                SELECT
                    frame.Timestamp,
                    2,
                    7,
                    'CORR_FRAME_CPU_LIMIT',
                    'CORRELATION:FRAME_CPU:' || frame.ProcessId,
                    frame.Application,
                    'Frametime-Spike mit CPU-Thread-Limit',
                    'Ein Frametime-Spike trat gleichzeitig mit einem CPU-Thread über 95 % und einer GPU-Auslastung unter 90 % auf.',
                    frame.FrameTimeMilliseconds,
                    'ms',
                    1
                FROM FrameMetricSamples frame
                WHERE frame.RunId = $runId
                  AND frame.ProcessId = $frameProcessId
                  AND frame.FrameTimeMilliseconds >= 25
                  AND EXISTS
                  (
                      SELECT 1
                      FROM CpuCoreMetricSamples core
                      WHERE core.RunId = frame.RunId
                        AND core.LoadPercent >= 95
                        AND ABS((julianday(core.Timestamp) - julianday(frame.Timestamp)) * 86400.0) <= 1.5
                  )
                  AND EXISTS
                  (
                      SELECT 1
                      FROM HardwareMetricSamples hardware
                      WHERE hardware.RunId = frame.RunId
                        AND COALESCE(hardware.GpuLoadPercent, 0) < 90
                        AND ABS((julianday(hardware.Timestamp) - julianday(frame.Timestamp)) * 86400.0) <= 1.5
                  )

                UNION ALL

                SELECT
                    frame.Timestamp,
                    2,
                    7,
                    'CORR_FRAME_DISK',
                    'CORRELATION:FRAME_DISK:' || frame.ProcessId,
                    frame.Application,
                    'Frametime-Spike mit Datenträgerlast',
                    'Ein Frametime-Spike trat gleichzeitig mit einer Datenträgeraktivität von mindestens 95 % auf.',
                    frame.FrameTimeMilliseconds,
                    'ms',
                    1
                FROM FrameMetricSamples frame
                WHERE frame.RunId = $runId
                  AND frame.ProcessId = $frameProcessId
                  AND frame.FrameTimeMilliseconds >= 25
                  AND EXISTS
                  (
                      SELECT 1
                      FROM DiskMetricSamples disk
                      WHERE disk.RunId = frame.RunId
                        AND disk.TotalActivityPercent >= 95
                        AND ABS((julianday(disk.Timestamp) - julianday(frame.Timestamp)) * 86400.0) <= 1.5
                  )

                UNION ALL

                SELECT
                    cores.Timestamp,
                    2,
                    7,
                    'CORR_CPU_LIMIT',
                    'CORRELATION:CPU_LIMIT',
                    'CPU/GPU-Korrelation',
                    'Mögliches CPU- oder Main-Thread-Limit',
                    'Ein CPU-Thread war am Limit, während die GPU unter 90 % ausgelastet war.',
                    cores.MaximumCoreLoad,
                    '% CPU-Thread',
                    1
                FROM
                (
                    SELECT Timestamp, MAX(LoadPercent) AS MaximumCoreLoad
                    FROM CpuCoreMetricSamples
                    WHERE RunId = $runId
                      AND LoadPercent >= 95
                    GROUP BY Timestamp
                ) cores
                WHERE EXISTS
                (
                    SELECT 1
                    FROM HardwareMetricSamples hardware
                    WHERE hardware.RunId = $runId
                      AND ABS(
                          (julianday(hardware.Timestamp) - julianday(cores.Timestamp)) * 86400.0
                      ) <= 1.5
                      AND COALESCE(hardware.GpuLoadPercent, 0) < 90
                )

                UNION ALL

                SELECT
                    disk.Timestamp,
                    2,
                    7,
                    'CORR_DISK_LOADING',
                    'CORRELATION:DISK_LOADING:' || disk.DeviceId || ':' || process.ProcessId,
                    disk.DeviceName || ' / ' || process.ProcessName,
                    'Möglicher Nachladevorgang',
                    'Hohe Datenträgeraktivität trat gleichzeitig mit hoher Prozess-Leserate auf.',
                    disk.TotalActivityPercent,
                    '% Datenträger',
                    1
                FROM DiskMetricSamples disk
                JOIN ProcessMetricSamples process
                  ON process.RunId = disk.RunId
                 AND ABS(
                     (julianday(process.Timestamp) - julianday(disk.Timestamp)) * 86400.0
                 ) <= 2.5
                WHERE disk.RunId = $runId
                  AND disk.TotalActivityPercent >= 95
                  AND process.ReadBytesPerSecond >= 26214400

                UNION ALL

                SELECT
                    process.Timestamp,
                    2,
                    7,
                    'CORR_BACKGROUND_CPU',
                    'CORRELATION:BACKGROUND_CPU:' || process.ProcessId,
                    process.ProcessName,
                    'Mögliche CPU-Konkurrenz durch Hintergrundprozess',
                    process.ProcessName || ' erzeugte hohe CPU-Last, während ein CPU-Thread am Limit war.',
                    process.CpuUsagePercent,
                    '% Prozess-CPU',
                    1
                FROM ProcessMetricSamples process
                WHERE process.RunId = $runId
                  AND process.IsForegroundProcess = 0
                  AND process.CpuUsagePercent >= 25
                  AND EXISTS
                  (
                      SELECT 1
                      FROM CpuCoreMetricSamples cores
                      WHERE cores.RunId = process.RunId
                        AND cores.LoadPercent >= 95
                        AND ABS(
                            (julianday(cores.Timestamp) - julianday(process.Timestamp)) * 86400.0
                        ) <= 2.5
                  )

                UNION ALL

                SELECT
                    network.Timestamp,
                    3,
                    7,
                    'CORR_LOCAL_NETWORK_FAILURE',
                    'CORRELATION:LOCAL_NETWORK',
                    'Lokales Netzwerk',
                    'Wahrscheinlich lokales Netzwerkproblem',
                    'Gateway und Internet waren gleichzeitig nicht erreichbar.',
                    network.GatewayConsecutiveFailures,
                    'Fehler',
                    1
                FROM NetworkMetricSamples network
                WHERE network.RunId = $runId
                  AND network.GatewaySuccess = 0
                  AND network.InternetSuccess = 0

                UNION ALL

                SELECT
                    memory.Timestamp,
                    2,
                    7,
                    'CORR_MEMORY_PRESSURE',
                    'CORRELATION:MEMORY_PRESSURE:' || process.ProcessId,
                    process.ProcessName,
                    'Möglicher Speicherdruck',
                    'Hohe Gesamtauslastung des RAM trat zusammen mit einem großen Prozess auf.',
                    memory.MemoryUsagePercent,
                    '% RAM',
                    1
                FROM SystemMetricSamples memory
                JOIN ProcessMetricSamples process
                  ON process.RunId = memory.RunId
                 AND ABS(
                     (julianday(process.Timestamp) - julianday(memory.Timestamp)) * 86400.0
                 ) <= 2.5
                WHERE memory.RunId = $runId
                  AND memory.MemoryUsagePercent >= 90
                  AND process.WorkingSetBytes >= 4294967296
            )
            ORDER BY Timestamp ASC, IsCorrelation DESC, Severity DESC
            LIMIT $maximumEventCount;
            """;

        command.Parameters.AddWithValue("$runId", runId.ToString());
        command.Parameters.AddWithValue(
            "$frameProcessId",
            likelyFrameProcessId);

        command.Parameters.AddWithValue(
            "$maximumEventCount",
            maximumEventCount);

        var events = new List<RunTimelineEvent>();

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            int numericValueOrdinal = reader.GetOrdinal("NumericValue");
            int unitOrdinal = reader.GetOrdinal("Unit");

            events.Add(
                new RunTimelineEvent(
                    RunId: runId,
                    Timestamp: ParseDateTime(
                        reader.GetString(reader.GetOrdinal("Timestamp"))),
                    Severity: (DiagnosticSeverity)reader.GetInt32(
                        reader.GetOrdinal("Severity")),
                    Category: (TimelineEventCategory)reader.GetInt32(
                        reader.GetOrdinal("Category")),
                    EventCode: reader.GetString(
                        reader.GetOrdinal("EventCode")),
                    SourceId: reader.GetString(
                        reader.GetOrdinal("SourceId")),
                    SourceName: reader.GetString(
                        reader.GetOrdinal("SourceName")),
                    Title: reader.GetString(
                        reader.GetOrdinal("Title")),
                    Description: reader.GetString(
                        reader.GetOrdinal("Description")),
                    NumericValue: reader.IsDBNull(numericValueOrdinal)
                        ? null
                        : reader.GetDouble(numericValueOrdinal),
                    Unit: reader.IsDBNull(unitOrdinal)
                        ? null
                        : reader.GetString(unitOrdinal),
                    IsCorrelation: reader.GetInt32(
                        reader.GetOrdinal("IsCorrelation")) == 1));
        }

        return events;
    }


    public async Task<IReadOnlyList<FrameChartPoint>>
        GetFrameChartPointsAsync(
            Guid runId,
            int maximumPointCount = 1500,
            CancellationToken cancellationToken = default)
    {
        if (maximumPointCount <= 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPointCount));
        }

        IReadOnlyList<FrameRunAnalysis> analyses =
            await GetFrameRunAnalysesAsync(
                runId,
                cancellationToken);

        FrameRunAnalysis? selected =
            analyses.FirstOrDefault(item =>
                item.IsLikelyGame)
            ?? analyses.FirstOrDefault();

        if (selected is null)
        {
            return [];
        }

        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                Timestamp,
                FramesPerSecond,
                FrameTimeMilliseconds
            FROM FrameMetricSamples
            WHERE RunId = $runId
              AND ProcessId = $processId
            ORDER BY Timestamp ASC;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        command.Parameters.AddWithValue(
            "$processId",
            selected.ProcessId);

        var raw =
            new List<(DateTimeOffset Timestamp,
                      double Fps,
                      double FrameTime)>();

        await using (
            SqliteDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken))
        {
            while (await reader.ReadAsync(
                       cancellationToken))
            {
                raw.Add((
                    ParseDateTime(
                        reader.GetString(
                            reader.GetOrdinal("Timestamp"))),
                    reader.GetDouble(
                        reader.GetOrdinal("FramesPerSecond")),
                    reader.GetDouble(
                        reader.GetOrdinal("FrameTimeMilliseconds"))));
            }
        }

        if (raw.Count == 0)
        {
            return [];
        }

        List<HardwareContextRow> hardware =
            await ReadHardwareContextAsync(
                connection,
                runId,
                cancellationToken);

        List<SystemContextRow> system =
            await ReadSystemContextAsync(
                connection,
                runId,
                cancellationToken);

        List<CpuContextRow> cpu =
            await ReadCpuContextAsync(
                connection,
                runId,
                cancellationToken);

        List<DiskContextRow> disks =
            await ReadDiskContextAsync(
                connection,
                runId,
                cancellationToken);

        List<NetworkContextRow> network =
            await ReadNetworkContextAsync(
                connection,
                runId,
                cancellationToken);

        List<BackgroundProcessContextRow> backgroundProcesses =
            await ReadBackgroundProcessContextAsync(
                connection,
                runId,
                selected.MatchedToProcessId,
                selected.ProcessId,
                cancellationToken);

        int step =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    raw.Count /
                    (double)maximumPointCount));

        DateTimeOffset startedAt =
            raw[0].Timestamp;

        var points =
            new List<FrameChartPoint>();

        for (int index = 0;
             index < raw.Count;
             index += step)
        {
            int bucketEnd =
                Math.Min(
                    raw.Count,
                    index + step);

            var bucket =
                raw.GetRange(
                    index,
                    bucketEnd - index);

            double averageFps =
                bucket.Average(item =>
                    item.Fps);

            double maximumFrameTime =
                bucket.Max(item =>
                    item.FrameTime);

            DateTimeOffset timestamp =
                bucket[bucket.Count / 2]
                    .Timestamp;

            HardwareContextRow? nearestHardware =
                FindNearest(
                    hardware,
                    timestamp,
                    item => item.Timestamp);

            SystemContextRow? nearestSystem =
                FindNearest(
                    system,
                    timestamp,
                    item => item.Timestamp);

            CpuContextRow? nearestCpu =
                FindNearest(
                    cpu,
                    timestamp,
                    item => item.Timestamp);

            DiskContextRow? nearestDisk =
                FindNearest(
                    disks,
                    timestamp,
                    item => item.Timestamp);

            NetworkContextRow? nearestNetwork =
                FindNearest(
                    network,
                    timestamp,
                    item => item.Timestamp);

            BackgroundProcessContextRow? nearestBackground =
                FindNearest(
                    backgroundProcesses,
                    timestamp,
                    item => item.Timestamp);

            points.Add(
                new FrameChartPoint(
                    Timestamp: timestamp,
                    SecondsFromStart:
                        (timestamp - startedAt)
                        .TotalSeconds,
                    FramesPerSecond:
                        averageFps,
                    FrameTimeMilliseconds:
                        maximumFrameTime,
                    IsSpike:
                        maximumFrameTime >= 25,
                    CpuUsagePercent:
                        nearestSystem?.CpuUsagePercent,
                    MaximumCpuThreadLoadPercent:
                        nearestCpu?.MaximumThreadLoadPercent,
                    GpuLoadPercent:
                        nearestHardware?.GpuLoadPercent,
                    MemoryUsagePercent:
                        nearestSystem?.MemoryUsagePercent,
                    VramUsagePercent:
                        nearestHardware?.VramUsagePercent,
                    DiskActivityPercent:
                        nearestDisk?.ActivityPercent,
                    DiskReadMegabytesPerSecond:
                        nearestDisk?.ReadMegabytesPerSecond,
                    GatewaySuccess:
                        nearestNetwork?.GatewaySuccess,
                    InternetSuccess:
                        nearestNetwork?.InternetSuccess,
                    BackgroundProcessName:
                        nearestBackground?.ProcessName,
                    BackgroundProcessCpuPercent:
                        nearestBackground?.CpuUsagePercent));
        }

        return points;
    }

    private static async Task<List<BackgroundProcessContextRow>>
        ReadBackgroundProcessContextAsync(
            SqliteConnection connection,
            Guid runId,
            int? mainProcessId,
            int frameProcessId,
            CancellationToken cancellationToken)
    {
        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                Timestamp,
                ProcessName,
                CpuUsagePercent
            FROM ProcessMetricSamples
            WHERE RunId = $runId
              AND IsForegroundProcess = 0
              AND ProcessId <> $frameProcessId
              AND ($mainProcessId IS NULL OR
                   ProcessId <> $mainProcessId)
            ORDER BY Timestamp ASC,
                     CpuUsagePercent DESC;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        command.Parameters.AddWithValue(
            "$frameProcessId",
            frameProcessId);

        command.Parameters.AddWithValue(
            "$mainProcessId",
            mainProcessId is null
                ? DBNull.Value
                : mainProcessId.Value);

        var allRows =
            new List<BackgroundProcessContextRow>();

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(
                   cancellationToken))
        {
            allRows.Add(
                new BackgroundProcessContextRow(
                    Timestamp:
                        ParseDateTime(
                            reader.GetString(0)),
                    ProcessName:
                        reader.GetString(1),
                    CpuUsagePercent:
                        reader.GetDouble(2)));
        }

        return allRows
            .GroupBy(row =>
                row.Timestamp)
            .Select(group =>
                group.OrderByDescending(row =>
                    row.CpuUsagePercent)
                .First())
            .OrderBy(row =>
                row.Timestamp)
            .ToList();
    }

    public async Task<IReadOnlyList<RunTimelineEvent>>
        GetPerformanceTimelineEventsAsync(
            Guid runId,
            int maximumEventCount = 500,
            CancellationToken cancellationToken = default)
    {
        if (maximumEventCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumEventCount));
        }

        IReadOnlyList<FrameRunAnalysis> analyses =
            await GetFrameRunAnalysesAsync(
                runId,
                cancellationToken);

        FrameRunAnalysis? selected =
            analyses.FirstOrDefault(item =>
                item.IsLikelyGame)
            ?? analyses.FirstOrDefault();

        if (selected is null)
        {
            return [];
        }

        await using SqliteConnection connection =
            await OpenConnectionAsync(cancellationToken);

        List<PerformanceSpikeRow> spikes =
            await ReadPerformanceSpikesAsync(
                connection,
                runId,
                selected.ProcessId,
                maximumEventCount,
                cancellationToken);

        List<HardwareContextRow> hardware =
            await ReadHardwareContextAsync(
                connection,
                runId,
                cancellationToken);

        List<SystemContextRow> system =
            await ReadSystemContextAsync(
                connection,
                runId,
                cancellationToken);

        List<CpuContextRow> cpu =
            await ReadCpuContextAsync(
                connection,
                runId,
                cancellationToken);

        List<DiskContextRow> disks =
            await ReadDiskContextAsync(
                connection,
                runId,
                cancellationToken);

        List<NetworkContextRow> network =
            await ReadNetworkContextAsync(
                connection,
                runId,
                cancellationToken);

        var events =
            new List<RunTimelineEvent>();

        foreach (PerformanceSpikeRow spike
                 in spikes)
        {
            HardwareContextRow? nearestHardware =
                FindNearest(
                    hardware,
                    spike.Timestamp,
                    item => item.Timestamp);

            SystemContextRow? nearestSystem =
                FindNearest(
                    system,
                    spike.Timestamp,
                    item => item.Timestamp);

            CpuContextRow? nearestCpu =
                FindNearest(
                    cpu,
                    spike.Timestamp,
                    item => item.Timestamp);

            DiskContextRow? nearestDisk =
                FindNearest(
                    disks,
                    spike.Timestamp,
                    item => item.Timestamp);

            NetworkContextRow? nearestNetwork =
                FindNearest(
                    network,
                    spike.Timestamp,
                    item => item.Timestamp);

            string causeCode;
            string causeTitle;
            string causeDescription;

            if (nearestNetwork is not null &&
                (!nearestNetwork.GatewaySuccess ||
                 !nearestNetwork.InternetSuccess))
            {
                causeCode =
                    "FRAME_CAUSE_NETWORK";

                causeTitle =
                    "Frametime-Spike mit Netzwerkstörung";

                causeDescription =
                    "Zum Zeitpunkt des Frametime-Spikes war " +
                    "mindestens ein Netzwerkziel nicht erreichbar.";
            }
            else if (nearestDisk?.ActivityPercent >= 95)
            {
                causeCode =
                    "FRAME_CAUSE_DISK";

                causeTitle =
                    "Frametime-Spike durch möglichen Nachladevorgang";

                causeDescription =
                    $"Datenträgeraktivität {nearestDisk.ActivityPercent:F1} %, " +
                    $"Lesen {nearestDisk.ReadMegabytesPerSecond:F1} MB/s.";
            }
            else if (nearestCpu?.MaximumThreadLoadPercent >= 95 &&
                     (nearestHardware?.GpuLoadPercent ?? 0) < 90)
            {
                causeCode =
                    "FRAME_CAUSE_CPU";

                causeTitle =
                    "Frametime-Spike durch mögliches CPU-/Main-Thread-Limit";

                causeDescription =
                    $"Stärkster CPU-Thread {nearestCpu.MaximumThreadLoadPercent:F1} %, " +
                    $"GPU {nearestHardware?.GpuLoadPercent ?? 0:F1} %.";
            }
            else if ((nearestHardware?.GpuLoadPercent ?? 0) >= 95)
            {
                causeCode =
                    "FRAME_CAUSE_GPU";

                causeTitle =
                    "Frametime-Spike bei GPU-Limit";

                causeDescription =
                    $"GPU-Auslastung {nearestHardware!.GpuLoadPercent:F1} %, " +
                    $"GPU-Temperatur {nearestHardware.GpuTemperatureCelsius:F1} °C.";
            }
            else if ((nearestSystem?.MemoryUsagePercent ?? 0) >= 90)
            {
                causeCode =
                    "FRAME_CAUSE_MEMORY";

                causeTitle =
                    "Frametime-Spike bei hohem Speicherdruck";

                causeDescription =
                    $"RAM-Auslastung {nearestSystem!.MemoryUsagePercent:F1} %.";
            }
            else
            {
                causeCode =
                    "FRAME_CAUSE_UNCLEAR";

                causeTitle =
                    "Frametime-Spike ohne eindeutige Ursache";

                causeDescription =
                    $"CPU {nearestSystem?.CpuUsagePercent ?? 0:F1} %, " +
                    $"GPU {nearestHardware?.GpuLoadPercent ?? 0:F1} %, " +
                    $"Datenträger {nearestDisk?.ActivityPercent ?? 0:F1} %.";
            }

            DiagnosticSeverity severity =
                spike.FrameTimeMilliseconds >= 100
                    ? DiagnosticSeverity.Critical
                    : spike.FrameTimeMilliseconds >= 50
                        ? DiagnosticSeverity.Warning
                        : DiagnosticSeverity.Information;

            events.Add(
                new RunTimelineEvent(
                    RunId: runId,
                    Timestamp: spike.Timestamp,
                    Severity: severity,
                    Category:
                        TimelineEventCategory.Correlation,
                    EventCode: causeCode,
                    SourceId:
                        $"FRAME:{selected.ProcessId}",
                    SourceName:
                        selected.MatchedToProcessName ??
                        selected.Application,
                    Title: causeTitle,
                    Description:
                        $"{causeDescription} " +
                        $"Frametime {spike.FrameTimeMilliseconds:F1} ms " +
                        $"({spike.FramesPerSecond:F1} FPS).",
                    NumericValue:
                        spike.FrameTimeMilliseconds,
                    Unit: "ms",
                    IsCorrelation: true));
        }

        return events;
    }

    private static async Task<List<PerformanceSpikeRow>>
        ReadPerformanceSpikesAsync(
            SqliteConnection connection,
            Guid runId,
            int processId,
            int maximumEventCount,
            CancellationToken cancellationToken)
    {
        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                Timestamp,
                FramesPerSecond,
                FrameTimeMilliseconds
            FROM FrameMetricSamples
            WHERE RunId = $runId
              AND ProcessId = $processId
              AND FrameTimeMilliseconds >= 25
            ORDER BY FrameTimeMilliseconds DESC
            LIMIT $maximumEventCount;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        command.Parameters.AddWithValue(
            "$processId",
            processId);

        command.Parameters.AddWithValue(
            "$maximumEventCount",
            maximumEventCount);

        var rows =
            new List<PerformanceSpikeRow>();

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(
                   cancellationToken))
        {
            rows.Add(
                new PerformanceSpikeRow(
                    ParseDateTime(
                        reader.GetString(
                            reader.GetOrdinal("Timestamp"))),
                    reader.GetDouble(
                        reader.GetOrdinal("FramesPerSecond")),
                    reader.GetDouble(
                        reader.GetOrdinal("FrameTimeMilliseconds"))));
        }

        return rows
            .OrderBy(item => item.Timestamp)
            .ToList();
    }

    private static async Task<List<HardwareContextRow>>
        ReadHardwareContextAsync(
            SqliteConnection connection,
            Guid runId,
            CancellationToken cancellationToken)
    {
        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT Timestamp,
                   COALESCE(GpuLoadPercent, 0),
                   COALESCE(GpuTemperatureCelsius, 0),
                   CASE
                       WHEN GpuTotalMemoryMegabytes IS NULL
                         OR GpuTotalMemoryMegabytes <= 0
                       THEN NULL
                       ELSE COALESCE(GpuUsedMemoryMegabytes, 0)
                            / GpuTotalMemoryMegabytes
                            * 100.0
                   END
            FROM HardwareMetricSamples
            WHERE RunId = $runId
            ORDER BY Timestamp;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        var rows =
            new List<HardwareContextRow>();

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(
                   cancellationToken))
        {
            rows.Add(
                new HardwareContextRow(
                    ParseDateTime(
                        reader.GetString(0)),
                    reader.GetDouble(1),
                    reader.GetDouble(2),
                    reader.IsDBNull(3)
                        ? null
                        : reader.GetDouble(3)));
        }

        return rows;
    }

    private static async Task<List<SystemContextRow>>
        ReadSystemContextAsync(
            SqliteConnection connection,
            Guid runId,
            CancellationToken cancellationToken)
    {
        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT Timestamp,
                   CpuUsagePercent,
                   MemoryUsagePercent
            FROM SystemMetricSamples
            WHERE RunId = $runId
            ORDER BY Timestamp;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        var rows =
            new List<SystemContextRow>();

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(
                   cancellationToken))
        {
            rows.Add(
                new SystemContextRow(
                    ParseDateTime(
                        reader.GetString(0)),
                    reader.GetDouble(1),
                    reader.GetDouble(2)));
        }

        return rows;
    }

    private static async Task<List<CpuContextRow>>
        ReadCpuContextAsync(
            SqliteConnection connection,
            Guid runId,
            CancellationToken cancellationToken)
    {
        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT Timestamp,
                   MAX(LoadPercent)
            FROM CpuCoreMetricSamples
            WHERE RunId = $runId
            GROUP BY Timestamp
            ORDER BY Timestamp;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        var rows =
            new List<CpuContextRow>();

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(
                   cancellationToken))
        {
            rows.Add(
                new CpuContextRow(
                    ParseDateTime(
                        reader.GetString(0)),
                    reader.IsDBNull(1)
                        ? 0
                        : reader.GetDouble(1)));
        }

        return rows;
    }

    private static async Task<List<DiskContextRow>>
        ReadDiskContextAsync(
            SqliteConnection connection,
            Guid runId,
            CancellationToken cancellationToken)
    {
        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT Timestamp,
                   MAX(COALESCE(TotalActivityPercent, 0)),
                   MAX(COALESCE(ReadBytesPerSecond, 0)) / 1048576.0
            FROM DiskMetricSamples
            WHERE RunId = $runId
            GROUP BY Timestamp
            ORDER BY Timestamp;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        var rows =
            new List<DiskContextRow>();

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(
                   cancellationToken))
        {
            rows.Add(
                new DiskContextRow(
                    ParseDateTime(
                        reader.GetString(0)),
                    reader.GetDouble(1),
                    reader.GetDouble(2)));
        }

        return rows;
    }

    private static async Task<List<NetworkContextRow>>
        ReadNetworkContextAsync(
            SqliteConnection connection,
            Guid runId,
            CancellationToken cancellationToken)
    {
        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT Timestamp,
                   GatewaySuccess,
                   InternetSuccess
            FROM NetworkMetricSamples
            WHERE RunId = $runId
            ORDER BY Timestamp;
            """;

        command.Parameters.AddWithValue(
            "$runId",
            runId.ToString());

        var rows =
            new List<NetworkContextRow>();

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(
                   cancellationToken))
        {
            rows.Add(
                new NetworkContextRow(
                    ParseDateTime(
                        reader.GetString(0)),
                    reader.GetInt32(1) == 1,
                    reader.GetInt32(2) == 1));
        }

        return rows;
    }

    private static T? FindNearest<T>(
        IReadOnlyList<T> rows,
        DateTimeOffset timestamp,
        Func<T, DateTimeOffset> timestampSelector)
        where T : class
    {
        T? best = null;
        TimeSpan bestDistance =
            TimeSpan.MaxValue;

        foreach (T row in rows)
        {
            TimeSpan distance =
                (timestampSelector(row) - timestamp)
                .Duration();

            if (distance < bestDistance)
            {
                best = row;
                bestDistance = distance;
            }
        }

        return bestDistance <=
               TimeSpan.FromSeconds(2.5)
            ? best
            : null;
    }

    private sealed record PerformanceSpikeRow(
        DateTimeOffset Timestamp,
        double FramesPerSecond,
        double FrameTimeMilliseconds);

    private sealed record HardwareContextRow(
        DateTimeOffset Timestamp,
        double GpuLoadPercent,
        double GpuTemperatureCelsius,
        double? VramUsagePercent);

    private sealed record SystemContextRow(
        DateTimeOffset Timestamp,
        double CpuUsagePercent,
        double MemoryUsagePercent);

    private sealed record CpuContextRow(
        DateTimeOffset Timestamp,
        double MaximumThreadLoadPercent);

    private sealed record DiskContextRow(
        DateTimeOffset Timestamp,
        double ActivityPercent,
        double ReadMegabytesPerSecond);

    private sealed record NetworkContextRow(
        DateTimeOffset Timestamp,
        bool GatewaySuccess,
        bool InternetSuccess);


    private sealed record BackgroundProcessContextRow(
        DateTimeOffset Timestamp,
        string ProcessName,
        double CpuUsagePercent);

}
