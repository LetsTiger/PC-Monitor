using System.Text.RegularExpressions;

namespace PcMonitor.Collectors.Hardware;

internal static partial class CpuLoadSensorParser
{
    [GeneratedRegex(
        @"^CPU Core #(?<core>\d+) Thread #(?<thread>\d+)$",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex SensorNameRegex();

    public static bool TryParse(
        string sensorName,
        out int coreIndex,
        out int threadIndex)
    {
        coreIndex = 0;
        threadIndex = 0;

        if (string.IsNullOrWhiteSpace(sensorName))
        {
            return false;
        }

        Match match =
            SensorNameRegex().Match(sensorName);

        if (!match.Success)
        {
            return false;
        }

        return
            int.TryParse(
                match.Groups["core"].Value,
                out coreIndex)
            &&
            int.TryParse(
                match.Groups["thread"].Value,
                out threadIndex);
    }
}