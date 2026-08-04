namespace PcMonitor.Core.Models;

public enum FrameProcessMatchReason
{
    NotSelected = 0,
    ExactProcessId = 1,
    ExactProcessName = 2,
    SharedInstallationDirectory = 3,
    SharedPublisherDirectory = 4,
    FallbackScore = 5
}
