using System.ComponentModel;
using System.Runtime.CompilerServices;
using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class DiskLiveViewModel :
    INotifyPropertyChanged
{
    private string _deviceName = "–";
    private string _temperatureText = "–";
    private string _activityText = "–";
    private string _readRateText = "–";
    private string _writeRateText = "–";
    private string _spaceText = "–";
    private string _lifeText = "–";
    private string _statusText = "–";

    public DiskLiveViewModel(
        string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException(
                "Die Datenträger-ID darf nicht leer sein.",
                nameof(deviceId));
        }

        DeviceId = deviceId;
    }

    public string DeviceId { get; }

    public string DeviceName
    {
        get => _deviceName;
        private set =>
            SetField(ref _deviceName, value);
    }

    public string TemperatureText
    {
        get => _temperatureText;
        private set =>
            SetField(ref _temperatureText, value);
    }

    public string ActivityText
    {
        get => _activityText;
        private set =>
            SetField(ref _activityText, value);
    }

    public string ReadRateText
    {
        get => _readRateText;
        private set =>
            SetField(ref _readRateText, value);
    }

    public string WriteRateText
    {
        get => _writeRateText;
        private set =>
            SetField(ref _writeRateText, value);
    }

    public string SpaceText
    {
        get => _spaceText;
        private set =>
            SetField(ref _spaceText, value);
    }

    public string LifeText
    {
        get => _lifeText;
        private set =>
            SetField(ref _lifeText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set =>
            SetField(ref _statusText, value);
    }

    public void Apply(
        DiskMetricsSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        if (!string.Equals(
                DeviceId,
                sample.DeviceId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Das Sample gehört zu einem anderen Datenträger.",
                nameof(sample));
        }

        DeviceName = sample.DeviceName;

        TemperatureText =
            FormatTemperature(
                sample.TemperatureCelsius);

        ActivityText =
            FormatPercent(
                sample.TotalActivityPercent);

        ReadRateText =
            FormatRate(
                sample.ReadBytesPerSecond);

        WriteRateText =
            FormatRate(
                sample.WriteBytesPerSecond);

        SpaceText =
            CreateSpaceText(sample);

        LifeText =
            sample.RemainingLifePercent is null
                ? "Nicht verfügbar"
                : $"{sample.RemainingLifePercent.Value:F0} %";

        StatusText =
            CreateStatusText(sample);
    }

    public event PropertyChangedEventHandler?
        PropertyChanged;

    private static string CreateSpaceText(
        DiskMetricsSample sample)
    {
        string usedText =
            sample.UsedSpacePercent is null
                ? "–"
                : $"{sample.UsedSpacePercent.Value:F1} % belegt";

        string freeText =
            sample.FreeSpaceGigabytes is null
                ? "freier Speicher unbekannt"
                : $"{sample.FreeSpaceGigabytes.Value:F1} GB frei";

        return $"{usedText}, {freeText}";
    }

    private static string CreateStatusText(
        DiskMetricsSample sample)
    {
        if (sample.TemperatureCelsius >= 80)
        {
            return "Sehr hohe Temperatur";
        }

        if (sample.TemperatureCelsius >= 70)
        {
            return "Erhöhte Temperatur";
        }

        if (sample.TotalActivityPercent >= 95)
        {
            return "Vollständig ausgelastet";
        }

        if (sample.UsedSpacePercent >= 95)
        {
            return "Fast vollständig belegt";
        }

        if (sample.UsedSpacePercent >= 90)
        {
            return "Wenig freier Speicher";
        }

        if (sample.RemainingLifePercent < 80)
        {
            return "Lebenszustand prüfen";
        }

        return "In Ordnung";
    }

    private static string FormatTemperature(
        double? value)
    {
        return value is null
            ? "Nicht verfügbar"
            : $"{value.Value:F1} °C";
    }

    private static string FormatPercent(
        double? value)
    {
        return value is null
            ? "Nicht verfügbar"
            : $"{value.Value:F1} %";
    }

    private static string FormatRate(
        double? bytesPerSecond)
    {
        if (bytesPerSecond is null)
        {
            return "Nicht verfügbar";
        }

        double value = bytesPerSecond.Value;

        if (value >= 1024d * 1024d * 1024d)
        {
            return
                $"{value / (1024d * 1024d * 1024d):F2} GB/s";
        }

        if (value >= 1024d * 1024d)
        {
            return
                $"{value / (1024d * 1024d):F1} MB/s";
        }

        if (value >= 1024d)
        {
            return
                $"{value / 1024d:F1} KB/s";
        }

        return $"{value:F0} B/s";
    }

    private bool SetField<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(
                field,
                value))
        {
            return false;
        }

        field = value;

        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));

        return true;
    }
}