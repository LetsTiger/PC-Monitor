using System.Threading.Channels;
using PcMonitor.Core.Metrics;
using PcMonitor.Core.Models;
using PcMonitor.Core.Storage;

namespace PcMonitor.Application.Storage;

/// <summary>
/// Nimmt Messwerte schnell entgegen und schreibt sie
/// gesammelt in einem separaten Hintergrundprozess
/// in die Datenbank.
/// </summary>
public sealed class MonitoringStorageCoordinator :
    IMonitoringStorageCoordinator,
    IAsyncDisposable
{
    private const int MaximumBatchSize = 100;

    private static readonly TimeSpan MaximumBatchDelay =
        TimeSpan.FromMilliseconds(500);

    private readonly IMonitoringStorage _storage;
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    private Channel<IMetricSample>? _sampleChannel;
    private CancellationTokenSource? _writerCancellation;
    private Task? _writerTask;

    private Guid? _currentRunId;
    private Exception? _writerException;

    private long _queuedSampleCount;
    private long _savedSampleCount;
    private long _rejectedSampleCount;

    private bool _disposed;

    public MonitoringStorageCoordinator(
        IMonitoringStorage storage)
    {
        _storage = storage
            ?? throw new ArgumentNullException(
                nameof(storage));
    }

    public bool IsRunning { get; private set; }

    public long QueuedSampleCount =>
        Interlocked.Read(
            ref _queuedSampleCount);

    public long SavedSampleCount =>
        Interlocked.Read(
            ref _savedSampleCount);

    /// <summary>
    /// Anzahl der Samples, die gar nicht in den Channel
    /// geschrieben werden konnten.
    ///
    /// Bei DropOldest können verdrängte ältere Samples
    /// damit nicht exakt gezählt werden.
    /// </summary>
    public long RejectedSampleCount =>
        Interlocked.Read(
            ref _rejectedSampleCount);

    public Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return _storage.InitializeAsync(
            cancellationToken);
    }

    public async Task StartRunAsync(
        MonitoringRun run,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(run);

        await _stateLock.WaitAsync(
            cancellationToken);

        try
        {
            if (IsRunning)
            {
                throw new InvalidOperationException(
                    "Die Speicherung eines Runs läuft bereits.");
            }

            await _storage.CreateRunAsync(
                run,
                cancellationToken);

            _sampleChannel =
                Channel.CreateBounded<IMetricSample>(
                    new BoundedChannelOptions(10_000)
                    {
                        SingleReader = true,
                        SingleWriter = false,

                        FullMode =
                            BoundedChannelFullMode.DropOldest
                    });

            _writerCancellation =
                new CancellationTokenSource();

            _currentRunId = run.Id;
            _writerException = null;

            Interlocked.Exchange(
                ref _queuedSampleCount,
                0);

            Interlocked.Exchange(
                ref _savedSampleCount,
                0);

            Interlocked.Exchange(
                ref _rejectedSampleCount,
                0);

            IsRunning = true;

            _writerTask =
                WriteSamplesAsync(
                    run.Id,
                    _sampleChannel.Reader,
                    _writerCancellation.Token);
        }
        catch
        {
            ResetRunState();
            throw;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public bool TryQueueSample(
        IMetricSample sample)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(sample);

        Channel<IMetricSample>? channel =
            _sampleChannel;

        if (!IsRunning ||
            channel is null)
        {
            Interlocked.Increment(
                ref _rejectedSampleCount);

            return false;
        }

        bool queued =
            channel.Writer.TryWrite(
                sample);

        if (queued)
        {
            Interlocked.Increment(
                ref _queuedSampleCount);

            return true;
        }

        Interlocked.Increment(
            ref _rejectedSampleCount);

        return false;
    }

    public async Task CompleteRunAsync(
        MonitoringRun run,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(run);

        await _stateLock.WaitAsync(
            cancellationToken);

        try
        {
            if (!IsRunning)
            {
                throw new InvalidOperationException(
                    "Es läuft keine Speicherung.");
            }

            if (_currentRunId != run.Id)
            {
                throw new InvalidOperationException(
                    "Der abzuschließende Run entspricht nicht " +
                    "dem aktuell gespeicherten Run.");
            }

            await FinishWriterAsync();

            ThrowIfWriterFailed();

            await _storage.CompleteRunAsync(
                run,
                cancellationToken);

            ResetRunState();
        }
        catch
        {
            ResetRunState();
            throw;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task AbortRunAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _stateLock.WaitAsync(
            cancellationToken);

        try
        {
            if (!IsRunning)
            {
                return;
            }

            _sampleChannel?
                .Writer
                .TryComplete();

            if (_writerCancellation is not null)
            {
                await _writerCancellation
                    .CancelAsync();
            }

            if (_writerTask is not null)
            {
                try
                {
                    await _writerTask;
                }
                catch (OperationCanceledException)
                {
                    // Erwartet beim Abbruch.
                }
                catch
                {
                    // Ein ursprünglicher Start- oder
                    // Collectorfehler ist relevanter.
                }
            }

            ResetRunState();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (IsRunning)
        {
            await AbortRunAsync();
        }

        _disposed = true;

        _stateLock.Dispose();
    }

    private async Task WriteSamplesAsync(
        Guid runId,
        ChannelReader<IMetricSample> reader,
        CancellationToken cancellationToken)
    {
        var batch =
            new List<IMetricSample>(
                MaximumBatchSize);

        DateTimeOffset lastFlushAt =
            DateTimeOffset.UtcNow;

        try
        {
            await foreach (
                IMetricSample sample
                in reader.ReadAllAsync(
                    cancellationToken))
            {
                batch.Add(sample);

                bool batchSizeReached =
                    batch.Count >= MaximumBatchSize;

                bool batchDelayReached =
                    DateTimeOffset.UtcNow -
                    lastFlushAt >=
                    MaximumBatchDelay;

                if (!batchSizeReached &&
                    !batchDelayReached)
                {
                    continue;
                }

                await FlushBatchAsync(
                    runId,
                    batch,
                    cancellationToken);

                lastFlushAt =
                    DateTimeOffset.UtcNow;
            }

            // Der Channel wurde regulär abgeschlossen.
            // Verbleibende Samples ebenfalls speichern.
            if (batch.Count > 0)
            {
                await FlushBatchAsync(
                    runId,
                    batch,
                    CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Beim Abbruch versuchen wir noch, alle bereits
            // im Channel befindlichen Samples einzusammeln.
            while (reader.TryRead(
                       out IMetricSample? remainingSample))
            {
                batch.Add(
                    remainingSample);

                if (batch.Count >= MaximumBatchSize)
                {
                    await FlushBatchAsync(
                        runId,
                        batch,
                        CancellationToken.None);
                }
            }

            if (batch.Count > 0)
            {
                await FlushBatchAsync(
                    runId,
                    batch,
                    CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            _writerException =
                exception;

            _sampleChannel?
                .Writer
                .TryComplete(
                    exception);
        }
    }

    private async Task FlushBatchAsync(
        Guid runId,
        List<IMetricSample> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        IMetricSample[] samples =
            batch.ToArray();

        // Erst nach erfolgreichem Kopieren leeren.
        // Das Array bleibt für den Datenbankaufruf erhalten.
        batch.Clear();

        try
        {
            await _storage.SaveSamplesAsync(
                runId,
                samples,
                cancellationToken);

            Interlocked.Add(
                ref _savedSampleCount,
                samples.Length);
        }
        catch
        {
            // Die Samples wieder vorne in den Arbeitspuffer
            // aufnehmen. Das hilft vor allem dabei, beim
            // anschließenden Fehlerzustand nichts still zu verlieren.
            batch.InsertRange(
                0,
                samples);

            throw;
        }
    }

    private async Task FinishWriterAsync()
    {
        _sampleChannel?
            .Writer
            .TryComplete();

        if (_writerTask is not null)
        {
            await _writerTask;
        }
    }

    private void ThrowIfWriterFailed()
    {
        if (_writerException is null)
        {
            return;
        }

        throw new InvalidOperationException(
            "Beim Speichern der Messwerte ist ein Fehler aufgetreten.",
            _writerException);
    }

    private void ResetRunState()
    {
        IsRunning = false;
        _currentRunId = null;

        _sampleChannel = null;
        _writerTask = null;

        _writerCancellation?.Dispose();
        _writerCancellation = null;

        _writerException = null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}