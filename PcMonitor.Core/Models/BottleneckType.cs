namespace PcMonitor.Core.Models;

public enum BottleneckType
{
    Unknown = 0,
    Gpu = 1,
    CpuMainThread = 2,
    Memory = 3,
    Vram = 4,
    DiskStreaming = 5,
    Network = 6,
    BackgroundProcess = 7,
    Mixed = 8
}
