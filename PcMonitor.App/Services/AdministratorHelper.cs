using System.Diagnostics;
using System.Security.Principal;

namespace PcMonitor.App.Services;

public static class AdministratorHelper
{
    public static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool TryRestartElevated(IReadOnlyList<string> arguments)
    {
        string? executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath)) return false;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = true,
                Verb = "runas"
            };

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            Process? process = Process.Start(startInfo);
            StartupTrace.Write($"Erhöhter Prozess gestartet. PID: {process?.Id.ToString() ?? "unbekannt"}.");
            return true;
        }
        catch (Exception exception)
        {
            StartupTrace.Write($"Erhöhter Prozess konnte nicht gestartet werden: {exception}");
            return false;
        }
    }

}
