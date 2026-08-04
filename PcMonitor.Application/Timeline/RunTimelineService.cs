using PcMonitor.Core.Diagnostics;
using PcMonitor.Core.Timeline;

namespace PcMonitor.Application.Timeline;

public sealed class RunTimelineService :
    IRunTimelineService
{
    private static readonly TimeSpan DefaultMaximumGap =
        TimeSpan.FromSeconds(3);

    public IReadOnlyList<RunTimelinePeriod> CreatePeriods(
        IReadOnlyList<RunTimelineEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0)
        {
            return [];
        }

        var periods = new List<RunTimelinePeriod>();

        foreach (IGrouping<TimelineGroupKey, RunTimelineEvent> group
                 in events
                     .OrderBy(item => item.Timestamp)
                     .GroupBy(item => new TimelineGroupKey(
                         item.RunId,
                         item.EventCode,
                         item.SourceId,
                         item.Unit,
                         item.IsCorrelation)))
        {
            PeriodBuilder? currentPeriod = null;

            foreach (RunTimelineEvent timelineEvent
                     in group.OrderBy(item => item.Timestamp))
            {
                if (currentPeriod is null)
                {
                    currentPeriod = new PeriodBuilder(timelineEvent);
                    continue;
                }

                if (CanMerge(currentPeriod, timelineEvent))
                {
                    currentPeriod.Add(timelineEvent);
                    continue;
                }

                periods.Add(currentPeriod.Build());
                currentPeriod = new PeriodBuilder(timelineEvent);
            }

            if (currentPeriod is not null)
            {
                periods.Add(currentPeriod.Build());
            }
        }

        return periods
            .Where(ShouldKeepPeriod)
            .OrderBy(period => period.StartedAt)
            .ThenByDescending(period => period.Severity)
            .ThenBy(period => period.Category)
            .ToArray();
    }

    private static bool CanMerge(
        PeriodBuilder period,
        RunTimelineEvent timelineEvent)
    {
        TimeSpan gap =
            timelineEvent.Timestamp - period.LastObservedAt;

        return gap >= TimeSpan.Zero &&
               gap <= GetMaximumGap(timelineEvent);
    }

    private static TimeSpan GetMaximumGap(
        RunTimelineEvent timelineEvent)
    {
        if (timelineEvent.Category == TimelineEventCategory.Process)
        {
            return TimeSpan.FromSeconds(5);
        }

        if (timelineEvent.Category == TimelineEventCategory.Network)
        {
            return TimeSpan.FromSeconds(4);
        }

        return DefaultMaximumGap;
    }

    private static bool ShouldKeepPeriod(
        RunTimelinePeriod period)
    {
        if (period.IsCorrelation ||
            period.Severity == DiagnosticSeverity.Critical)
        {
            return true;
        }

        if (period.EventCount >= 2)
        {
            return true;
        }

        return period.EventCode switch
        {
            "NETWORK_GATEWAY_FAILURE" => true,
            "NETWORK_INTERNET_FAILURE" => true,
            "PROCESS_FOREGROUND_CHANGED" => true,
            "PROCESS_CPU_HIGH" =>
                period.MaximumNumericValue >= 50,
            "PROCESS_READ_HIGH" =>
                period.MaximumNumericValue >= 250,
            "PROCESS_WRITE_HIGH" =>
                period.MaximumNumericValue >= 100,
            "DISK_ACTIVITY_HIGH" =>
                period.MaximumNumericValue >= 99,
            _ => false
        };
    }

    private sealed record TimelineGroupKey(
        Guid RunId,
        string EventCode,
        string SourceId,
        string? Unit,
        bool IsCorrelation);

    private sealed class PeriodBuilder
    {
        private DiagnosticSeverity _severity;
        private string _description;
        private int _eventCount;
        private double _numericValueSum;
        private int _numericValueCount;
        private double? _maximumNumericValue;
        private TimeSpan _estimatedInterval;

        public PeriodBuilder(
            RunTimelineEvent timelineEvent)
        {
            RunId = timelineEvent.RunId;
            StartedAt = timelineEvent.Timestamp;
            LastObservedAt = timelineEvent.Timestamp;
            Category = timelineEvent.Category;
            EventCode = timelineEvent.EventCode;
            SourceId = timelineEvent.SourceId;
            SourceName = timelineEvent.SourceName;
            Title = timelineEvent.Title;
            Unit = timelineEvent.Unit;
            IsCorrelation = timelineEvent.IsCorrelation;
            _severity = timelineEvent.Severity;
            _description = timelineEvent.Description;
            _eventCount = 1;
            _estimatedInterval = GetInitialInterval(timelineEvent);

            AddNumericValue(timelineEvent.NumericValue);
        }

        public Guid RunId { get; }
        public DateTimeOffset StartedAt { get; }
        public DateTimeOffset LastObservedAt { get; private set; }
        public TimelineEventCategory Category { get; }
        public string EventCode { get; }
        public string SourceId { get; }
        public string SourceName { get; }
        public string Title { get; }
        public string? Unit { get; }
        public bool IsCorrelation { get; }

        public void Add(
            RunTimelineEvent timelineEvent)
        {
            TimeSpan interval =
                timelineEvent.Timestamp - LastObservedAt;

            if (interval > TimeSpan.Zero &&
                interval <= TimeSpan.FromSeconds(10))
            {
                _estimatedInterval = interval;
            }

            LastObservedAt = timelineEvent.Timestamp;
            _eventCount++;

            if (timelineEvent.Severity > _severity)
            {
                _severity = timelineEvent.Severity;
            }

            if (!string.IsNullOrWhiteSpace(timelineEvent.Description))
            {
                _description = timelineEvent.Description;
            }

            AddNumericValue(timelineEvent.NumericValue);
        }

        public RunTimelinePeriod Build()
        {
            DateTimeOffset endedAt =
                LastObservedAt + _estimatedInterval;

            double? averageNumericValue =
                _numericValueCount == 0
                    ? null
                    : _numericValueSum / _numericValueCount;

            return new RunTimelinePeriod(
                RunId: RunId,
                StartedAt: StartedAt,
                EndedAt: endedAt,
                Severity: _severity,
                Category: Category,
                EventCode: EventCode,
                SourceId: SourceId,
                SourceName: SourceName,
                Title: Title,
                Description: _description,
                EventCount: _eventCount,
                AverageNumericValue: averageNumericValue,
                MaximumNumericValue: _maximumNumericValue,
                Unit: Unit,
                IsCorrelation: IsCorrelation);
        }

        private void AddNumericValue(double? value)
        {
            if (value is null)
            {
                return;
            }

            _numericValueSum += value.Value;
            _numericValueCount++;

            if (_maximumNumericValue is null ||
                value.Value > _maximumNumericValue.Value)
            {
                _maximumNumericValue = value.Value;
            }
        }

        private static TimeSpan GetInitialInterval(
            RunTimelineEvent timelineEvent)
        {
            return timelineEvent.Category == TimelineEventCategory.Process
                ? TimeSpan.FromSeconds(2)
                : TimeSpan.FromSeconds(1);
        }
    }
}
