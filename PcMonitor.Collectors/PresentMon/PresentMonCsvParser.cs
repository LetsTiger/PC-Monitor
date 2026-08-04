using System.Globalization;
using System.Text;
using PcMonitor.Core.Models;

namespace PcMonitor.Collectors.PresentMon;

internal sealed class PresentMonCsvParser
{
    private Dictionary<string, int>? _columns;

    public bool TryParseLine(
        string line,
        DateTimeOffset captureStartedAt,
        out FrameMetricsSample? sample)
    {
        sample = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        string[] values = ParseCsvLine(line);

        if (_columns is null)
        {
            if (!values.Any(value =>
                    string.Equals(
                        value.Trim(),
                        "Application",
                        StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            _columns = values
                .Select((name, index) => new
                {
                    Name = name.Trim().TrimStart('\uFEFF'),
                    Index = index
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .GroupBy(
                    item => item.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().Index,
                    StringComparer.OrdinalIgnoreCase);

            return false;
        }

        if (!TryGetString(values, "Application", out string? application) ||
            string.IsNullOrWhiteSpace(application) ||
            !TryGetInt32(values, "ProcessID", out int processId) ||
            !TryGetDouble(values, "MsBetweenPresents", out double frameTime) ||
            frameTime <= 0 ||
            frameTime > 10_000)
        {
            return false;
        }

        DateTimeOffset timestamp;

        if (TryGetDateTime(
                values,
                "CPUStartDateTime",
                out DateTimeOffset absoluteTimestamp))
        {
            timestamp = absoluteTimestamp;
        }
        else if (TryGetDouble(
                     values,
                     "TimeInMs",
                     out double timeInMilliseconds))
        {
            timestamp =
                captureStartedAt.AddMilliseconds(
                    timeInMilliseconds);
        }
        else
        {
            timestamp = captureStartedAt;
        }

        double fps =
            1000.0 / frameTime;

        TryGetString(
            values,
            "PresentRuntime",
            out string? presentRuntime);

        TryGetString(
            values,
            "PresentMode",
            out string? presentMode);

        sample = new FrameMetricsSample(
            Timestamp: timestamp,
            Application: application.Trim(),
            ProcessId: processId,
            PresentRuntime: NullIfEmpty(presentRuntime),
            PresentMode: NullIfEmpty(presentMode),
            FrameTimeMilliseconds: frameTime,
            FramesPerSecond: fps,
            CpuBusyMilliseconds:
                GetNullableDouble(values, "MsCPUBusy"),
            GpuTimeMilliseconds:
                GetNullableDouble(values, "MsGPUTime"),
            DisplayLatencyMilliseconds:
                GetNullableDouble(values, "DisplayLatency"));

        return true;
    }

    private static string? NullIfEmpty(
        string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private double? GetNullableDouble(
        IReadOnlyList<string> values,
        string columnName)
    {
        return TryGetDouble(
            values,
            columnName,
            out double value)
                ? value
                : null;
    }

    private bool TryGetString(
        IReadOnlyList<string> values,
        string columnName,
        out string? value)
    {
        value = null;

        if (_columns is null ||
            !_columns.TryGetValue(
                columnName,
                out int index) ||
            index < 0 ||
            index >= values.Count)
        {
            return false;
        }

        value = values[index].Trim();
        return true;
    }

    private bool TryGetInt32(
        IReadOnlyList<string> values,
        string columnName,
        out int value)
    {
        value = 0;

        return TryGetString(
                   values,
                   columnName,
                   out string? text) &&
               int.TryParse(
                   text,
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out value);
    }

    private bool TryGetDouble(
        IReadOnlyList<string> values,
        string columnName,
        out double value)
    {
        value = 0;

        return TryGetString(
                   values,
                   columnName,
                   out string? text) &&
               !string.Equals(
                   text,
                   "NA",
                   StringComparison.OrdinalIgnoreCase) &&
               double.TryParse(
                   text,
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out value);
    }

    private bool TryGetDateTime(
        IReadOnlyList<string> values,
        string columnName,
        out DateTimeOffset value)
    {
        value = default;

        if (!TryGetString(
                values,
                columnName,
                out string? text) ||
            string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out value);
    }

    private static string[] ParseCsvLine(
        string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        bool insideQuotes = false;

        for (int index = 0;
             index < line.Length;
             index++)
        {
            char character = line[index];

            if (character == '"')
            {
                if (insideQuotes &&
                    index + 1 < line.Length &&
                    line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }

                continue;
            }

            if (character == ',' &&
                !insideQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        values.Add(current.ToString());
        return values.ToArray();
    }
}
