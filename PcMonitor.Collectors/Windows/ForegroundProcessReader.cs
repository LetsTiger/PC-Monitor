using System.Runtime.InteropServices;

namespace PcMonitor.Collectors.Windows;

internal static class ForegroundProcessReader
{
    public static int? TryGetProcessId()
    {
        IntPtr windowHandle =
            GetForegroundWindow();

        if (windowHandle == IntPtr.Zero)
        {
            return null;
        }

        _ = GetWindowThreadProcessId(
            windowHandle,
            out uint processId);

        return processId == 0
            ? null
            : checked((int)processId);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr windowHandle,
        out uint processId);
}