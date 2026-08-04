using Microsoft.Data.Sqlite;
using PcMonitor.Core.Models;
using PcMonitor.Core.Services;
using PcMonitor.Infrastructure.Storage;

namespace PcMonitor.Tests;

public sealed class SqliteRecoveryIntegrationTests
{
    [Fact]
    public async Task RecoverInterruptedRunsAsync_CompletesRunningRun()
    {
        string directory =
            Path.Combine(
                Path.GetTempPath(),
                "PcMonitorTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            directory);

        string databasePath =
            Path.Combine(
                directory,
                "pcmonitor.db");

        try
        {
            var storage =
                new SqliteMonitoringStorage(
                    new MonitoringDatabaseOptions(
                        databasePath));

            await storage.InitializeAsync();

            var runService =
                new RunService();

            MonitoringRun run =
                runService.StartRun(
                    "Recovery-Test");

            await storage.CreateRunAsync(
                run);

            DateTimeOffset recoveredAt =
                DateTimeOffset.UtcNow
                    .AddMinutes(1);

            int recoveredCount =
                await storage
                    .RecoverInterruptedRunsAsync(
                        recoveredAt);

            Assert.Equal(
                1,
                recoveredCount);

            RunSummary? recoveredRun =
                await storage.GetRunAsync(
                    run.Id);

            Assert.NotNull(
                recoveredRun);

            Assert.Equal(
                RunStatus.Completed,
                recoveredRun.Status);

            Assert.NotNull(
                recoveredRun.EndedAt);
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            await DeleteDirectoryWithRetryAsync(
                directory);
        }
    }

    private static async Task DeleteDirectoryWithRetryAsync(
        string directory)
    {
        const int maximumAttempts = 10;

        for (int attempt = 1;
             attempt <= maximumAttempts;
             attempt++)
        {
            try
            {
                if (Directory.Exists(
                        directory))
                {
                    Directory.Delete(
                        directory,
                        recursive: true);
                }

                return;
            }
            catch (IOException)
                when (attempt < maximumAttempts)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(
                        attempt * 100));
            }
            catch (UnauthorizedAccessException)
                when (attempt < maximumAttempts)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(
                        attempt * 100));
            }
        }

        if (Directory.Exists(
                directory))
        {
            Directory.Delete(
                directory,
                recursive: true);
        }
    }
}