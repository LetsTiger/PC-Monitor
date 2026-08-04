namespace PcMonitor.Core.Models;

public sealed record GameProfile(
    string Id,
    string DisplayName,
    string Description,
    double FpsWeight,
    double LowFpsWeight,
    double FrameTimeWeight,
    double StabilityWeight,
    double HardwareWeight,
    double NetworkWeight,
    double TargetFramesPerSecond,
    double AcceptableP99FrameTimeMilliseconds);
