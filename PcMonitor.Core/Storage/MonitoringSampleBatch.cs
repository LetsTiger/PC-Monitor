using PcMonitor.Core.Metrics;

namespace PcMonitor.Core.Storage;

public sealed class MonitoringSampleBatch
{
    public MonitoringSampleBatch(
        Guid runId,
        IReadOnlyList<IMetricSample> samples)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException(
                "Die Run-ID darf nicht leer sein.",
                nameof(runId));
        }

        ArgumentNullException.ThrowIfNull(samples);

        RunId = runId;
        Samples = samples;
    }

    public Guid RunId { get; }

    public IReadOnlyList<IMetricSample> Samples { get; }
}