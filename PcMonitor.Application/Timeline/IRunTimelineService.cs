using PcMonitor.Core.Timeline;

namespace PcMonitor.Application.Timeline;

public interface IRunTimelineService
{
    IReadOnlyList<RunTimelinePeriod> CreatePeriods(
        IReadOnlyList<RunTimelineEvent> events);
}