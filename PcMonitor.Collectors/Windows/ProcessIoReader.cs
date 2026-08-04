using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PcMonitor.Collectors.Windows;

internal static class ProcessIoReader
{
    public static bool TryRead(
        Process process,
        out ulong readBytes,
        out ulong writeBytes)
    {
        readBytes = 0;
        writeBytes = 0;

        try
        {
            if (!GetProcessIoCounters(
                    process.Handle,
                    out IoCounters counters))
            {
                return false;
            }

            readBytes = counters.ReadTransferCount;
            writeBytes = counters.WriteTransferCount;

            return true;
        }
        catch
        {
            return false;
        }
    }

    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessIoCounters(
        IntPtr processHandle,
        out IoCounters ioCounters);

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;

        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }
}