using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using PcMonitor.App.Services;
using PcMonitor.App.ViewModels;
using PcMonitor.Application.Analysis;
using PcMonitor.Application.Collectors;
using PcMonitor.Application.Diagnostics;
using PcMonitor.Application.Export;
using PcMonitor.Application.History;
using PcMonitor.Application.Monitoring;
using PcMonitor.Application.Network;
using PcMonitor.Application.Settings;
using PcMonitor.Application.Storage;
using PcMonitor.Application.Timeline;
using PcMonitor.Collectors.Hardware;
using PcMonitor.Collectors.PresentMon;
using PcMonitor.Collectors.Windows;
using PcMonitor.Core.Collectors;
using PcMonitor.Core.Models;
using PcMonitor.Core.Services;
using PcMonitor.Core.Storage;
using PcMonitor.Infrastructure.Settings;
using PcMonitor.Infrastructure.Storage;

namespace PcMonitor.App;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceName = "Local\\LetsTiger.PcMonitor.SingleInstance";
    private SingleInstanceGuard? _singleInstanceGuard;
    private Window? _startupWindow;
    private bool _restartRequested;

    protected override void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);
        StartupTrace.Enable(eventArgs.Args);
        StartupTrace.Write("OnStartup betreten.");

        // Während des Starts können Berechtigungs- und Übergabedialoge vor dem
        // Hauptfenster geschlossen werden. Sie dürfen nicht das automatische
        // Beenden der gesamten WPF-Anwendung auslösen.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _startupWindow = CreateStartupWindow();
        _startupWindow.Show();
        StartupTrace.Write("Unsichtbares Startfenster als Dispatcher-Lebensdaueranker geöffnet.");

        RegisterGlobalExceptionHandlers();
        StartupTrace.Write("Globale Exception-Handler registriert.");
        if (!WaitForPreviousProcess(eventArgs.Args))
        {
            StartupTrace.Write("Warten auf vorherigen Prozess mit Timeout beendet.");
            MessageBox.Show(
                "Die vorherige PC-Monitor-Instanz konnte nicht rechtzeitig beendet werden.",
                ReleaseInfo.ProductName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        _singleInstanceGuard = new SingleInstanceGuard(SingleInstanceName);
        StartupTrace.Write($"Single-Instance-Guard erstellt. Primärinstanz: {_singleInstanceGuard.IsPrimaryInstance}.");
        if (!_singleInstanceGuard.IsPrimaryInstance)
        {
            StartupTrace.Write("Dialog zur Übernahme der laufenden Instanz wird angezeigt.");
            MessageBoxResult replacementResult = MessageBox.Show(
                "PC Monitor wird bereits ausgeführt. Soll die laufende Instanz beendet und PC Monitor neu gestartet werden?",
                ReleaseInfo.ProductName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (replacementResult != MessageBoxResult.Yes ||
                !_singleInstanceGuard.RequestReplacement(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30)))
            {
                StartupTrace.Write($"Übernahme abgebrochen oder fehlgeschlagen. Auswahl: {replacementResult}.");
                MessageBox.Show(
                    "Die laufende Instanz wurde nicht beendet. Dieser Start wird abgebrochen.",
                    ReleaseInfo.ProductName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown();
                return;
            }

            StartupTrace.Write("Übernahme der laufenden Instanz erfolgreich.");
        }

        StartReplacementListener();
        StartupTrace.Write("Replacement-Listener gestartet.");

        string[] arguments = eventArgs.Args.ToArray();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            StartupTrace.Write("Asynchroner Anwendungsstart wird vom laufenden Dispatcher ausgeführt.");
            _ = StartApplicationSafelyAsync(arguments);
        }));
        StartupTrace.Write("Asynchroner Anwendungsstart wurde beim Dispatcher eingeplant.");
    }

    private async Task StartApplicationSafelyAsync(IReadOnlyList<string> arguments)
    {
        try
        {
            StartupTrace.Write("StartApplication wird aufgerufen.");
            await StartApplicationAsync(arguments);
        }
        catch (Exception exception)
        {
            StartupTrace.Write($"StartApplication fehlgeschlagen: {exception}");
            AppLog.Error("PC Monitor konnte nicht gestartet werden.", exception);
            MessageBox.Show(
                "PC Monitor konnte nicht gestartet werden. Details wurden im Anwendungsprotokoll gespeichert.\n\n" + exception.Message,
                ReleaseInfo.ProductName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs eventArgs)
    {
        StartupTrace.Write($"OnExit betreten. ExitCode: {eventArgs.ApplicationExitCode}.");
        AppLog.Information("PC Monitor wird beendet.");
        _singleInstanceGuard?.Dispose();
        StartupTrace.Write("Single-Instance-Guard freigegeben.");
        base.OnExit(eventArgs);
    }

    private async Task StartApplicationAsync(IReadOnlyList<string> arguments)
    {
        StartupTrace.Write("Anwendungsdienste werden initialisiert.");
        string applicationDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PcMonitor");
        Directory.CreateDirectory(applicationDataDirectory);

        string settingsPath = Path.Combine(applicationDataDirectory, "settings.json");
        IApplicationSettingsService settingsService = new JsonApplicationSettingsService(settingsPath);
        PcMonitorSettings settings = await settingsService.LoadAsync();
        StartupTrace.Write($"Einstellungen geladen. PresentMon aktiviert: {settings.PresentMonEnabled}; Startpräferenz: {settings.PresentMonStartupPreference}.");

        bool presentMonEnabledForSession = await ResolvePresentMonModeAsync(settings, settingsService, arguments);
        StartupTrace.Write($"PresentMon-Modus aufgelöst. Sitzung aktiviert: {presentMonEnabledForSession}; Neustart angefordert: {_restartRequested}.");
        if (_restartRequested)
        {
            StartupTrace.Write("StartApplication wird wegen erhöhtem Neustart verlassen.");
            return;
        }
        settings = await settingsService.LoadAsync();

        string databasePath = string.IsNullOrWhiteSpace(settings.DatabasePath)
            ? Path.Combine(applicationDataDirectory, "pcmonitor.db")
            : settings.DatabasePath;

        IRunService runService = new RunService();
        IRunTimelineService runTimelineService = new RunTimelineService();
        var collectors = new List<IMetricCollector>
        {
            new SystemMetricsCollector(TimeSpan.FromMilliseconds(settings.SystemIntervalMilliseconds)),
            new NetworkMetricsCollector("1.1.1.1", TimeSpan.FromMilliseconds(settings.NetworkIntervalMilliseconds), TimeSpan.FromMilliseconds(800)),
            new HardwareMetricsCollector(TimeSpan.FromMilliseconds(settings.HardwareIntervalMilliseconds)),
            new DiskMetricsCollector(TimeSpan.FromMilliseconds(settings.DiskIntervalMilliseconds)),
            new ProcessMetricsCollector(TimeSpan.FromMilliseconds(settings.ProcessIntervalMilliseconds), settings.MaximumProcessesPerSample)
        };

        if (presentMonEnabledForSession)
        {
            collectors.Add(new PresentMonMetricsCollector(
                PresentMonOptions.CreateDefault() with { CircularBufferSize = settings.PresentMonCircularBufferSize }));
        }

        ICollectorManager collectorManager = new CollectorManager(collectors);
        IMonitoringStorage monitoringStorage = new SqliteMonitoringStorage(new MonitoringDatabaseOptions(databasePath));
        StartupTrace.Write($"Storage wird initialisiert: {databasePath}");
        int recoveredRuns = Task.Run(async () =>
            {
                StartupTrace.Write("Storage-Initialisierung auf dem Threadpool begonnen.");
                await monitoringStorage.InitializeAsync().ConfigureAwait(false);
                StartupTrace.Write("Storage-Initialisierung abgeschlossen; unterbrochene Runs werden wiederhergestellt.");
                int result = await monitoringStorage
                    .RecoverInterruptedRunsAsync(DateTimeOffset.UtcNow)
                    .ConfigureAwait(false);
                StartupTrace.Write($"Wiederherstellung unterbrochener Runs abgeschlossen: {result}.");
                return result;
            })
            .GetAwaiter()
            .GetResult();
        StartupTrace.Write("Threadpool-Ergebnis wurde auf dem UI-Thread übernommen.");
        AppLog.Information($"Storage initialisiert. Wiederhergestellte Runs: {recoveredRuns}.");

        INetworkIncidentDetector networkIncidentDetector = new NetworkIncidentDetector(monitoringStorage);
        IMonitoringStorageCoordinator storageCoordinator = new MonitoringStorageCoordinator(monitoringStorage);
        IMonitoringSessionService monitoringSessionService = new MonitoringSessionService(runService, collectorManager, storageCoordinator, networkIncidentDetector);
        IRunHistoryService runHistoryService = new RunHistoryService(monitoringStorage);
        IRunAnalysisService runAnalysisService = new RunAnalysisService(monitoringStorage);
        IRunExportService runExportService = new RunExportService(monitoringStorage);
        IFileSaveDialogService fileSaveDialogService = new FileSaveDialogService();
        IConfirmationDialogService confirmationDialogService = new ConfirmationDialogService();
        IRunDiagnosticService runDiagnosticService = new RunDiagnosticService();
        IPerformanceAnalysisService performanceAnalysisService = new PerformanceAnalysisService();
        IAnalysisQualityService analysisQualityService = new AnalysisQualityService();
        IDiagnosticsPackageService diagnosticsService = new DiagnosticsPackageService(settingsService, monitoringStorage, databasePath);

        var viewModel = new MainViewModel(
            monitoringSessionService, runHistoryService, runExportService, fileSaveDialogService,
            confirmationDialogService, runAnalysisService, runDiagnosticService, runTimelineService,
            performanceAnalysisService, analysisQualityService, settingsService, settings,
            diagnosticsService, presentMonEnabledForSession);

        var mainWindow = new MainWindow(viewModel);
        StartupTrace.Write("MainWindow wurde erstellt.");
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
        StartupTrace.Write($"MainWindow.Show abgeschlossen. Sichtbar: {mainWindow.IsVisible}.");
        _startupWindow?.Close();
        _startupWindow = null;
        StartupTrace.Write("Unsichtbares Startfenster geschlossen.");
        AppLog.Information($"{ReleaseInfo.ProductName} {ReleaseInfo.ProductVersion} gestartet. PresentMon-Sitzung: {presentMonEnabledForSession}.");
    }

    private static Window CreateStartupWindow() =>
        new()
        {
            Width = 1,
            Height = 1,
            Left = -10000,
            Top = -10000,
            Opacity = 0,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize
        };

    private async Task<bool> ResolvePresentMonModeAsync(
        PcMonitorSettings settings,
        IApplicationSettingsService settingsService,
        IReadOnlyList<string> arguments)
    {
        if (!settings.PresentMonEnabled)
        {
            StartupTrace.Write("PresentMon ist in den Einstellungen deaktiviert.");
            return false;
        }
        if (arguments.Contains("--presentmon-disabled", StringComparer.OrdinalIgnoreCase))
        {
            StartupTrace.Write("PresentMon wurde per Startargument deaktiviert.");
            return false;
        }
        if (AdministratorHelper.IsAdministrator())
        {
            StartupTrace.Write("Prozess besitzt Administratorrechte; PresentMon wird aktiviert.");
            return true;
        }

        bool startWithPresentMon;
        if (settings.PresentMonStartupPreference == PresentMonStartupPreference.Ask)
        {
            var dialog = new PresentMonPermissionDialog();
            StartupTrace.Write("PresentMon-Berechtigungsdialog wird angezeigt.");
            dialog.ShowDialog();
            startWithPresentMon = dialog.StartWithPresentMon;
            StartupTrace.Write($"PresentMon-Berechtigungsdialog geschlossen. Mit PresentMon: {startWithPresentMon}; Auswahl merken: {dialog.RememberChoice}.");

            if (dialog.RememberChoice)
            {
                PresentMonStartupPreference preference = startWithPresentMon
                    ? PresentMonStartupPreference.WithPresentMon
                    : PresentMonStartupPreference.WithoutPresentMon;
                await settingsService.SaveAsync(settings with { PresentMonStartupPreference = preference });
            }
        }
        else
        {
            startWithPresentMon = settings.PresentMonStartupPreference == PresentMonStartupPreference.WithPresentMon;
        }

        var elevatedArguments = new List<string> { "--presentmon-enabled" };
        if (StartupTrace.IsEnabled) elevatedArguments.Add("--startup-debug");

        StartupTrace.Write($"Erhöhter Neustart wird versucht: {startWithPresentMon}.");
        if (startWithPresentMon && AdministratorHelper.TryRestartElevated(elevatedArguments))
        {
            StartupTrace.Write("Erhöhte Instanz wurde gestartet; Guard wird freigegeben.");
            _singleInstanceGuard?.Dispose();
            _singleInstanceGuard = null;
            _restartRequested = true;
            StartupTrace.Write("Shutdown der ursprünglichen Instanz wird angefordert.");
            Shutdown();
            return false;
        }

        AppLog.Information("PresentMon wurde für diese Sitzung deaktiviert, weil keine erhöhten Rechte verfügbar sind.");
        return false;
    }

    private void StartReplacementListener()
    {
        _singleInstanceGuard!.StartReplacementListener(
            () => Dispatcher.Invoke(() =>
                MessageBox.Show(
                    "Eine weitere Instanz möchte PC Monitor neu starten. Soll die laufende Instanz wirklich beendet werden?",
                    ReleaseInfo.ProductName,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No) == MessageBoxResult.Yes),
            () => Dispatcher.BeginInvoke(RequestShutdownForReplacement));
    }

    private void RequestShutdownForReplacement()
    {
        if (MainWindow is not null)
        {
            MainWindow.Close();
            return;
        }

        Shutdown();
    }

    private static bool WaitForPreviousProcess(IReadOnlyList<string> arguments)
    {
        const string prefix = "--wait-for-process=";
        string? argument = arguments.FirstOrDefault(value =>
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (argument is null || !int.TryParse(argument[prefix.Length..], out int processId))
        {
            StartupTrace.Write("Kein gültiges Argument zum Warten auf einen vorherigen Prozess vorhanden.");
            return true;
        }

        StartupTrace.Write($"Warten auf vorherigen Prozess PID {processId} beginnt.");

        try
        {
            using Process process = Process.GetProcessById(processId);
            if (!process.WaitForExit((int)TimeSpan.FromSeconds(30).TotalMilliseconds))
            {
                StartupTrace.Write($"Vorheriger Prozess PID {processId} wurde nicht rechtzeitig beendet.");
                return false;
            }

            StartupTrace.Write($"Vorheriger Prozess PID {processId} wurde beendet.");
        }
        catch (ArgumentException)
        {
            // Der vorherige Prozess wurde bereits beendet.
            StartupTrace.Write($"Vorheriger Prozess PID {processId} existiert bereits nicht mehr.");
        }

        return true;
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("Unbehandelte AppDomain-Ausnahme.", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Unbeobachtete Task-Ausnahme.", args.Exception);
            args.SetObserved();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        AppLog.Error("Unbehandelte UI-Ausnahme.", eventArgs.Exception);
        MessageBox.Show(
            "Ein unerwarteter Fehler ist aufgetreten. Details wurden im Anwendungsprotokoll gespeichert.",
            ReleaseInfo.ProductName,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        eventArgs.Handled = true;
    }
}
