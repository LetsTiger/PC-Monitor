namespace PcMonitor.Collectors.PresentMon;

public sealed record PresentMonOptions(
    string ExecutablePath,
    string SessionNamePrefix = "PcMonitor",
    bool ExcludeDroppedFrames = true,
    int CircularBufferSize = 65_536,
    TimeSpan FilePollInterval = default,
    TimeSpan ShutdownTimeout = default)
{
    public TimeSpan EffectiveFilePollInterval =>
        FilePollInterval <= TimeSpan.Zero
            ? TimeSpan.FromMilliseconds(100)
            : FilePollInterval;

    public TimeSpan EffectiveShutdownTimeout =>
        ShutdownTimeout <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(3)
            : ShutdownTimeout;

    public static PresentMonOptions CreateDefault()
    {
        string executablePath = Path.Combine(
            AppContext.BaseDirectory,
            "Tools",
            "PresentMon",
            "PresentMon.exe");

        return new PresentMonOptions(
            ExecutablePath: executablePath);
    }
}
