using PcMonitor.Core.Collectors;
using PcMonitor.Core.Metrics;

namespace PcMonitor.Application.Collectors;

public sealed class CollectorManager :
    ICollectorManager,
    IAsyncDisposable
{
    private readonly IReadOnlyList<IMetricCollector> _collectors;
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    public CollectorManager(
        IEnumerable<IMetricCollector> collectors)
    {
        ArgumentNullException.ThrowIfNull(collectors);

        _collectors = collectors.ToList();

        if (_collectors.Count == 0)
        {
            throw new ArgumentException(
                "Mindestens ein Collector muss registriert sein.",
                nameof(collectors));
        }

        foreach (IMetricCollector collector in _collectors)
        {
            collector.SampleCollected += OnSampleCollected;
        }
    }

    public bool IsRunning { get; private set; }

    public IReadOnlyCollection<string> CollectorNames =>
        _collectors
            .Select(collector => collector.Name)
            .ToArray();

    public event EventHandler<IMetricSample>? SampleCollected;

    public async Task StartAllAsync(
        CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);

        try
        {
            if (IsRunning)
            {
                return;
            }

            var startedCollectors = new List<IMetricCollector>();

            try
            {
                foreach (IMetricCollector collector in _collectors)
                {
                    await collector.StartAsync(cancellationToken);
                    startedCollectors.Add(collector);
                }

                IsRunning = true;
            }
            catch
            {
                foreach (IMetricCollector collector
                         in startedCollectors.AsEnumerable().Reverse())
                {
                    await collector.StopAsync();
                }

                throw;
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task StopAllAsync()
    {
        await _stateLock.WaitAsync();

        try
        {
            if (!IsRunning)
            {
                return;
            }

            List<Exception>? errors = null;

            foreach (IMetricCollector collector
                     in _collectors.Reverse())
            {
                try
                {
                    await collector.StopAsync();
                }
                catch (Exception exception)
                {
                    errors ??= [];
                    errors.Add(exception);
                }
            }

            IsRunning = false;

            if (errors is not null)
            {
                throw new AggregateException(
                    "Mindestens ein Collector konnte nicht sauber gestoppt werden.",
                    errors);
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAllAsync();
        }
        finally
        {
            foreach (IMetricCollector collector in _collectors)
            {
                collector.SampleCollected -= OnSampleCollected;

                if (collector is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (collector is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _stateLock.Dispose();
        }
    }

    private void OnSampleCollected(
        object? sender,
        IMetricSample sample)
    {
        SampleCollected?.Invoke(sender, sample);
    }
}