using System.Text;
using LibreHardwareMonitor.Hardware;

namespace PcMonitor.Collectors.Hardware;

internal static class HardwareSensorDiagnostic
{
    public static string CreateSensorReport(
        IEnumerable<IHardware> hardwareItems)
    {
        var builder = new StringBuilder();

        foreach (IHardware hardware in hardwareItems)
        {
            AppendHardware(
                builder,
                hardware,
                indentationLevel: 0);
        }

        return builder.ToString();
    }

    private static void AppendHardware(
        StringBuilder builder,
        IHardware hardware,
        int indentationLevel)
    {
        string indentation =
            new(' ', indentationLevel * 4);

        builder.AppendLine(
            $"{indentation}Hardware: {hardware.Name}");

        builder.AppendLine(
            $"{indentation}Typ: {hardware.HardwareType}");

        foreach (ISensor sensor in hardware.Sensors)
        {
            builder.AppendLine(
                $"{indentation}  Sensor: {sensor.Name}");

            builder.AppendLine(
                $"{indentation}    Typ: {sensor.SensorType}");

            builder.AppendLine(
                $"{indentation}    Wert: " +
                $"{FormatValue(sensor.Value)}");

            builder.AppendLine(
                $"{indentation}    Min: " +
                $"{FormatValue(sensor.Min)}");

            builder.AppendLine(
                $"{indentation}    Max: " +
                $"{FormatValue(sensor.Max)}");
        }

        foreach (IHardware subHardware
                 in hardware.SubHardware)
        {
            AppendHardware(
                builder,
                subHardware,
                indentationLevel + 1);
        }
    }

    private static string FormatValue(float? value)
    {
        return value.HasValue
            ? value.Value.ToString("F3")
            : "NULL";
    }
}