namespace PcMonitor.Core.Models;
public sealed record RunApplicationOverride(
 Guid RunId, int MainProcessId, string MainProcessName,
 int FrameProcessId, string FrameApplication,
 bool RememberForFutureRuns, DateTimeOffset UpdatedAt);
