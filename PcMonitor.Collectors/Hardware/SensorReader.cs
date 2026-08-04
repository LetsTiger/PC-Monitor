using LibreHardwareMonitor.Hardware;

namespace PcMonitor.Collectors.Hardware;

internal static class SensorReader
{
    public static double? FindValue(
        IHardware hardware,
        SensorType sensorType,
        params string[] preferredNames)
    {
        IEnumerable<ISensor> sensors =
            EnumerateSensors(hardware)
                .Where(sensor =>
                    sensor.SensorType == sensorType &&
                    sensor.Value.HasValue);

        ISensor? preferredSensor =
            FindPreferredSensor(
                sensors,
                preferredNames);

        return preferredSensor?.Value;
    }

    public static double? FindMaximumValue(
        IHardware hardware,
        SensorType sensorType,
        Func<ISensor, bool>? filter = null)
    {
        IEnumerable<ISensor> sensors =
            EnumerateSensors(hardware)
                .Where(sensor =>
                    sensor.SensorType == sensorType &&
                    sensor.Value.HasValue);

        if (filter is not null)
        {
            sensors = sensors.Where(filter);
        }

        float[] values =
            sensors
                .Select(sensor => sensor.Value!.Value)
                .ToArray();

        return values.Length == 0
            ? null
            : values.Max();
    }

    public static double? FindAverageValue(
        IHardware hardware,
        SensorType sensorType,
        Func<ISensor, bool>? filter = null)
    {
        IEnumerable<ISensor> sensors =
            EnumerateSensors(hardware)
                .Where(sensor =>
                    sensor.SensorType == sensorType &&
                    sensor.Value.HasValue);

        if (filter is not null)
        {
            sensors = sensors.Where(filter);
        }

        float[] values =
            sensors
                .Select(sensor => sensor.Value!.Value)
                .ToArray();

        return values.Length == 0
            ? null
            : values.Average();
    }

    public static bool NameContains(
        ISensor sensor,
        params string[] fragments)
    {
        return fragments.Any(fragment =>
            sensor.Name.Contains(
                fragment,
                StringComparison.OrdinalIgnoreCase));
    }

    private static ISensor? FindPreferredSensor(
        IEnumerable<ISensor> sensors,
        IReadOnlyList<string> preferredNames)
    {
        ISensor[] sensorArray =
            sensors.ToArray();

        foreach (string preferredName in preferredNames)
        {
            ISensor? exactMatch =
                sensorArray.FirstOrDefault(sensor =>
                    string.Equals(
                        sensor.Name,
                        preferredName,
                        StringComparison.OrdinalIgnoreCase));

            if (exactMatch is not null)
            {
                return exactMatch;
            }
        }

        foreach (string preferredName in preferredNames)
        {
            ISensor? partialMatch =
                sensorArray.FirstOrDefault(sensor =>
                    sensor.Name.Contains(
                        preferredName,
                        StringComparison.OrdinalIgnoreCase));

            if (partialMatch is not null)
            {
                return partialMatch;
            }
        }

        return sensorArray.FirstOrDefault();
    }

    private static IEnumerable<ISensor> EnumerateSensors(
        IHardware hardware)
    {
        foreach (ISensor sensor in hardware.Sensors)
        {
            yield return sensor;
        }

        foreach (IHardware subHardware
                 in hardware.SubHardware)
        {
            foreach (ISensor sensor
                     in EnumerateSensors(subHardware))
            {
                yield return sensor;
            }
        }
    }
}