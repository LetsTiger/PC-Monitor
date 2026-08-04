using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using PcMonitor.App.Commands;
using PcMonitor.Application.Monitoring;
using PcMonitor.Core.Metrics;
using PcMonitor.Core.Models;
using System.Collections.ObjectModel;
using PcMonitor.Application.History;
using PcMonitor.App.Services;
using PcMonitor.Application.Export;
using WpfApplication = System.Windows.Application;
using PcMonitor.Application.Analysis;
using PcMonitor.Application.Diagnostics;
using PcMonitor.Core.Diagnostics;
using PcMonitor.Core.Timeline;
using PcMonitor.Application.Timeline;
using PcMonitor.Application.Settings;

namespace PcMonitor.App.ViewModels;

public sealed class MainViewModel :
    INotifyPropertyChanged,
    IAsyncDisposable
{
    private readonly IMonitoringSessionService
        _monitoringSessionService;

    private readonly DispatcherTimer _durationTimer;

    private readonly RelayCommand _startRunCommand;
    private readonly RelayCommand _stopRunCommand;

    private string _runName;
    private string _statusText = "Bereit";
    private string _durationText = "00:00:00";
    private string _startedAtText = "–";
    private string _endedAtText = "–";

    private string _cpuUsageText = "–";
    private string _memoryUsageText = "–";
    private string _memoryDetailsText = "–";
    private string _lastMeasurementText = "–";

    private bool _isRunning;
    private bool _isBusy;
    
    private readonly IRunHistoryService _runHistoryService;

    private RunSummaryViewModel? _selectedRun;
    private int _analysisLoadVersion;
    private bool _hasStartedInitialAnalysisLoad;

    private string _historyStatusText =
        "Run-Historie wird geladen …";

    private bool _isHistoryLoading;
    
    private readonly IRunExportService
        _runExportService;

    private readonly IFileSaveDialogService
        _fileSaveDialogService;

    private readonly RelayCommand
        _exportCsvCommand;

    private readonly RelayCommand
        _exportJsonCommand;

    private string _exportStatusText = string.Empty;

    private bool _isExporting;
    
    private readonly IConfirmationDialogService
        _confirmationDialogService;

    private readonly RelayCommand
        _renameRunCommand;

    private readonly RelayCommand
        _deleteRunCommand;

    private string _selectedRunName = string.Empty;

    private string _historyActionStatusText =
        string.Empty;

    private bool _isHistoryActionRunning;
    
    private string _gatewayAddressText = "–";
    private string _gatewayLatencyText = "–";
    private string _internetLatencyText = "–";
    private string _networkStatusText = "–";
    
    private string _gatewayPacketLossText = "–";
    private string _gatewayJitterText = "–";

    private string _internetPacketLossText = "–";
    private string _internetJitterText = "–";

    private string _networkFailureText = "–";
    
    private readonly IRunAnalysisService
        _runAnalysisService;

    private RunAnalysisViewModel?
        _selectedRunAnalysis;

    private string _analysisStatusText =
        "Kein Run ausgewählt.";

    private bool _isAnalysisLoading;
    private bool _isRunSelectionLoading;
    
    public ObservableCollection<NetworkIncidentViewModel>
        SelectedRunNetworkIncidents
    {
        get;
    } = [];
    
    private string _cpuNameText = "–";
    private string _cpuTemperatureText = "–";
    private string _cpuCoreTemperatureText = "–";
    private string _cpuClockText = "–";
    private string _cpuPowerText = "–";

    private string _gpuNameText = "–";
    private string _gpuLoadText = "–";
    private string _gpuTemperatureText = "–";
    private string _gpuHotspotText = "–";
    private string _gpuCoreClockText = "–";
    private string _gpuMemoryClockText = "–";
    private string _gpuPowerText = "–";
    private string _gpuMemoryText = "–";
    
    public ObservableCollection<DiskRunAnalysisViewModel>
        SelectedRunDiskAnalyses
    {
        get;
    } = [];
    
    public ObservableCollection<DiskLiveViewModel>
        LiveDisks
    {
        get;
    } = [];
    
    public ObservableCollection<ProcessRunAnalysisViewModel>
        SelectedRunProcessAnalyses
    {
        get;
    } = [];
    
    private string _dominantProcessText = "–";

    public string DominantProcessText
    {
        get => _dominantProcessText;
        private set =>
            SetField(
                ref _dominantProcessText,
                value);
    }
    
    public ObservableCollection<CpuCoreRunAnalysisViewModel>
        SelectedRunCpuCoreAnalyses
    {
        get;
    } = [];
    
    private string _cpuThreadAssessmentText = "–";
    private string _mostLoadedCpuThreadText = "–";
    
    public string CpuThreadAssessmentText
    {
        get => _cpuThreadAssessmentText;
        private set =>
            SetField(
                ref _cpuThreadAssessmentText,
                value);
    }

    public string MostLoadedCpuThreadText
    {
        get => _mostLoadedCpuThreadText;
        private set =>
            SetField(
                ref _mostLoadedCpuThreadText,
                value);
    }
    
    private readonly IRunDiagnosticService
        _runDiagnosticService;
    
    public ObservableCollection<DiagnosticFindingViewModel>
        SelectedRunDiagnosticFindings
    {
        get;
    } = [];
    
    private string _diagnosticSummaryText = "–";
    private string _diagnosticStatusText = "Keine Diagnose geladen.";
    
    private RunSampleCountsViewModel?
        _selectedRunSampleCounts;
    
    public RunSampleCountsViewModel?
        SelectedRunSampleCounts
    {
        get => _selectedRunSampleCounts;
        private set =>
            SetField(
                ref _selectedRunSampleCounts,
                value);
    }
    
    public ObservableCollection<RunTimelineEventViewModel>
        SelectedRunTimelineEvents
    {
        get;
    } = [];
    
    private string _timelineStatusText =
        "Keine Timeline geladen.";
    
    public string TimelineStatusText
    {
        get => _timelineStatusText;
        private set =>
            SetField(
                ref _timelineStatusText,
                value);
    }
    
    private readonly IRunTimelineService
        _runTimelineService;

    private readonly IPerformanceAnalysisService
        _performanceAnalysisService;
    
    public ObservableCollection<RunTimelinePeriodViewModel>
        SelectedRunTimelinePeriods
    {
        get;
    } = [];
    
    private string _currentFpsText = "–";
    private string _currentFrameTimeText = "–";
    private string _frameCaptureStatusText =
        "PresentMon wartet auf dargestellte Frames.";

    public ObservableCollection<FrameRunAnalysisViewModel>
        SelectedRunFrameAnalyses
    {
        get;
    } = [];

    public ObservableCollection<FrameChartPoint>
        SelectedRunFrameChartPoints
    {
        get;
    } = [];


    public ObservableCollection<StutterEpisodeViewModel>
        SelectedRunStutterEpisodes
    {
        get;
    } = [];

    public ObservableCollection<BottleneckDistributionItemViewModel>
        SelectedRunBottleneckDistribution
    {
        get;
    } = [];

    private string _stutterSummaryText =
        "Keine Stutter-Analyse geladen.";

    public string StutterSummaryText
    {
        get => _stutterSummaryText;
        private set =>
            SetField(
                ref _stutterSummaryText,
                value);
    }


    private PerformanceScoreViewModel?
        _selectedRunPerformanceScore;

    public PerformanceScoreViewModel?
        SelectedRunPerformanceScore
    {
        get => _selectedRunPerformanceScore;
        private set =>
            SetField(
                ref _selectedRunPerformanceScore,
                value);
    }

    public ObservableCollection<PerformanceRecommendationViewModel>
        SelectedRunRecommendations
    {
        get;
    } = [];

    public ObservableCollection<TimelineStoryViewModel>
        SelectedRunTimelineStories
    {
        get;
    } = [];

    public ObservableCollection<PerformanceHeatmapCellViewModel>
        SelectedRunHeatmap
    {
        get;
    } = [];

    private PerformanceRegressionViewModel?
        _selectedRunRegression;

    public PerformanceRegressionViewModel?
        SelectedRunRegression
    {
        get => _selectedRunRegression;
        private set =>
            SetField(
                ref _selectedRunRegression,
                value);
    }

    private GameSessionStatisticsViewModel?
        _selectedGameSessionStatistics;

    public GameSessionStatisticsViewModel?
        SelectedGameSessionStatistics
    {
        get => _selectedGameSessionStatistics;
        private set =>
            SetField(
                ref _selectedGameSessionStatistics,
                value);
    }

    public ObservableCollection<PerformanceTrendPointViewModel>
        SelectedGameTrends
    {
        get;
    } = [];

    private string _selectedGameProfileText = "–";

    public string SelectedGameProfileText
    {
        get => _selectedGameProfileText;
        private set =>
            SetField(
                ref _selectedGameProfileText,
                value);
    }

    private BottleneckAnalysisViewModel?
        _selectedRunBottleneck;

    public BottleneckAnalysisViewModel?
        SelectedRunBottleneck
    {
        get => _selectedRunBottleneck;
        private set =>
            SetField(
                ref _selectedRunBottleneck,
                value);
    }

    private RunSummaryViewModel?
        _selectedComparisonRun;

    public RunSummaryViewModel?
        SelectedComparisonRun
    {
        get => _selectedComparisonRun;
        set
        {
            if (!SetField(
                    ref _selectedComparisonRun,
                    value))
            {
                return;
            }

            _ = LoadRunComparisonAsync();
        }
    }

    private RunComparisonViewModel?
        _selectedRunComparison;

    public RunComparisonViewModel?
        SelectedRunComparison
    {
        get => _selectedRunComparison;
        private set =>
            SetField(
                ref _selectedRunComparison,
                value);
    }

    private string _comparisonStatusText =
        "Vergleichsrun auswählen.";

    public string ComparisonStatusText
    {
        get => _comparisonStatusText;
        private set =>
            SetField(
                ref _comparisonStatusText,
                value);
    }

    private string _timelineSummaryText = "–";

    public string TimelineSummaryText
    {
        get => _timelineSummaryText;
        private set =>
            SetField(
                ref _timelineSummaryText,
                value);
    }

    public string CurrentFpsText
    {
        get => _currentFpsText;
        private set => SetField(ref _currentFpsText, value);
    }

    public string CurrentFrameTimeText
    {
        get => _currentFrameTimeText;
        private set => SetField(ref _currentFrameTimeText, value);
    }

    public string FrameCaptureStatusText
    {
        get => _frameCaptureStatusText;
        private set => SetField(ref _frameCaptureStatusText, value);
    }

    private readonly IAnalysisQualityService _analysisQualityService;
    private readonly IApplicationSettingsService _settingsService;
    private readonly IDiagnosticsPackageService _diagnosticsPackageService;
    private readonly RelayCommand _createDiagnosticsPackageCommand;
    private readonly bool _presentMonEnabledForSession;
    private readonly RelayCommand _saveSettingsCommand;
    private readonly RelayCommand _applyApplicationOverrideCommand;
    public SettingsViewModel Settings { get; }
    public AnalysisQualityReport? SelectedRunAnalysisQuality { get; private set; }
    public string AnalysisVersionText => $"Analyse {AnalysisVersionInfo.AnalysisVersion} · Regeln {AnalysisVersionInfo.RuleSetVersion}";
    public ProcessRunAnalysisViewModel? SelectedManualMainProcess { get; set; }
    public FrameRunAnalysisViewModel? SelectedManualFrameProcess { get; set; }
    public bool RememberManualApplicationMapping { get; set; } = true;
    public string ManualApplicationSelectionStatusText { get; private set; } = "";
    public ICommand SaveSettingsCommand => _saveSettingsCommand;
    public ICommand ApplyApplicationOverrideCommand => _applyApplicationOverrideCommand;
    public ICommand CreateDiagnosticsPackageCommand => _createDiagnosticsPackageCommand;
    public string ProductVersionText => $"{ReleaseInfo.ProductName} {ReleaseInfo.ProductVersion}";
    public string RuntimeVersionText => $"Analyse {AnalysisVersionInfo.AnalysisVersion} · Regeln {AnalysisVersionInfo.RuleSetVersion} · Datenbank {ReleaseInfo.DatabaseSchemaVersion}";
    public string PresentMonSessionText => _presentMonEnabledForSession ? "FPS-Erfassung ist für diese Sitzung aktiv." : "FPS-Erfassung ist für diese Sitzung deaktiviert.";

    private string _diagnosticsStatusText = "Noch kein Diagnosepaket erstellt.";
    public string DiagnosticsStatusText
    {
        get => _diagnosticsStatusText;
        private set => SetField(ref _diagnosticsStatusText, value);
    }

    public MainViewModel(
        IMonitoringSessionService monitoringSessionService,
        IRunHistoryService runHistoryService,
        IRunExportService runExportService,
        IFileSaveDialogService fileSaveDialogService,
        IConfirmationDialogService confirmationDialogService,
        IRunAnalysisService runAnalysisService,
        IRunDiagnosticService runDiagnosticService,
        IRunTimelineService runTimelineService,
        IPerformanceAnalysisService performanceAnalysisService,
        IAnalysisQualityService analysisQualityService,
        IApplicationSettingsService settingsService,
        PcMonitorSettings initialSettings,
        IDiagnosticsPackageService diagnosticsPackageService,
        bool presentMonEnabledForSession)
    {
        _monitoringSessionService =
            monitoringSessionService
            ?? throw new ArgumentNullException(
                nameof(monitoringSessionService));
        
        _runHistoryService = runHistoryService
                             ?? throw new ArgumentNullException(
                                 nameof(runHistoryService));
        
        _runExportService =
            runExportService
            ?? throw new ArgumentNullException(
                nameof(runExportService));

        _fileSaveDialogService =
            fileSaveDialogService
            ?? throw new ArgumentNullException(
                nameof(fileSaveDialogService));

        _monitoringSessionService.SampleCollected +=
            OnSampleCollected;

        _runName =
            _monitoringSessionService.GenerateRunName();
        
        _confirmationDialogService =
            confirmationDialogService
            ?? throw new ArgumentNullException(
                nameof(confirmationDialogService));
        
        _runAnalysisService =
            runAnalysisService
            ?? throw new ArgumentNullException(
                nameof(runAnalysisService));
        
        _runDiagnosticService =
            runDiagnosticService
            ?? throw new ArgumentNullException(
                nameof(runDiagnosticService));
        
        _runTimelineService =
            runTimelineService
            ?? throw new ArgumentNullException(
                nameof(runTimelineService));

        _performanceAnalysisService = performanceAnalysisService ?? throw new ArgumentNullException(nameof(performanceAnalysisService));
        _analysisQualityService = analysisQualityService ?? throw new ArgumentNullException(nameof(analysisQualityService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _diagnosticsPackageService = diagnosticsPackageService ?? throw new ArgumentNullException(nameof(diagnosticsPackageService));
        _presentMonEnabledForSession = presentMonEnabledForSession;
        Settings = new SettingsViewModel(settingsService, initialSettings);
        _saveSettingsCommand = new RelayCommand(_ => SaveSettings(), _ => !IsRunning && !IsBusy);
        _applyApplicationOverrideCommand = new RelayCommand(_ => ApplyApplicationOverride(), _ => SelectedRun is not null && SelectedManualMainProcess is not null && SelectedManualFrameProcess is not null);
        _createDiagnosticsPackageCommand = new RelayCommand(_ => CreateDiagnosticsPackage(), _ => !IsRunning && !IsBusy);

        _startRunCommand = new RelayCommand(
            _ => StartRun(),
            _ => !IsRunning && !IsBusy);

        _stopRunCommand = new RelayCommand(
            _ => StopRun(),
            _ => IsRunning && !IsBusy);
        
        _exportCsvCommand =
            new RelayCommand(
                _ => ExportSelectedRunCsv(),
                _ => CanExportSelectedRun());

        _exportJsonCommand =
            new RelayCommand(
                _ => ExportSelectedRunJson(),
                _ => CanExportSelectedRun());
        
        _renameRunCommand =
            new RelayCommand(
                _ => RenameSelectedRun(),
                _ => CanModifySelectedRun());

        _deleteRunCommand =
            new RelayCommand(
                _ => DeleteSelectedRun(),
                _ => CanModifySelectedRun());
        
        _renameRunCommand =
            new RelayCommand(
                _ => RenameSelectedRun(),
                _ => CanRenameSelectedRun());

        _deleteRunCommand =
            new RelayCommand(
                _ => DeleteSelectedRun(),
                _ => CanDeleteSelectedRun());

        _durationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _durationTimer.Tick += OnDurationTimerTick;
        
        _ = LoadRunHistoryAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand StartRunCommand => _startRunCommand;

    public ICommand StopRunCommand => _stopRunCommand;
    
    public ICommand ExportCsvCommand =>
        _exportCsvCommand;

    public ICommand ExportJsonCommand =>
        _exportJsonCommand;
    
    public ICommand RenameRunCommand =>
        _renameRunCommand;

    public ICommand DeleteRunCommand =>
        _deleteRunCommand;

    public string RunName
    {
        get => _runName;
        set
        {
            if (!SetField(ref _runName, value))
            {
                return;
            }

            if (IsRunning)
            {
                _monitoringSessionService
                    .RenameCurrentRun(value);
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public string DurationText
    {
        get => _durationText;
        private set => SetField(ref _durationText, value);
    }

    public string StartedAtText
    {
        get => _startedAtText;
        private set => SetField(ref _startedAtText, value);
    }

    public string EndedAtText
    {
        get => _endedAtText;
        private set => SetField(ref _endedAtText, value);
    }

    public string CpuUsageText
    {
        get => _cpuUsageText;
        private set => SetField(ref _cpuUsageText, value);
    }

    public string MemoryUsageText
    {
        get => _memoryUsageText;
        private set => SetField(ref _memoryUsageText, value);
    }

    public string MemoryDetailsText
    {
        get => _memoryDetailsText;
        private set => SetField(ref _memoryDetailsText, value);
    }

    public string LastMeasurementText
    {
        get => _lastMeasurementText;
        private set => SetField(ref _lastMeasurementText, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (!SetField(ref _isRunning, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsNotRunning));
            RefreshCommands();
        }
    }

    public bool IsNotRunning => !IsRunning;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                RefreshCommands();
            }
        }
    }
    
    public string CpuNameText
    {
        get => _cpuNameText;
        private set => SetField(ref _cpuNameText, value);
    }

    public string CpuTemperatureText
    {
        get => _cpuTemperatureText;
        private set => SetField(ref _cpuTemperatureText, value);
    }

    public string CpuCoreTemperatureText
    {
        get => _cpuCoreTemperatureText;
        private set => SetField(ref _cpuCoreTemperatureText, value);
    }

    public string CpuClockText
    {
        get => _cpuClockText;
        private set => SetField(ref _cpuClockText, value);
    }

    public string CpuPowerText
    {
        get => _cpuPowerText;
        private set => SetField(ref _cpuPowerText, value);
    }

    public string GpuNameText
    {
        get => _gpuNameText;
        private set => SetField(ref _gpuNameText, value);
    }

    public string GpuLoadText
    {
        get => _gpuLoadText;
        private set => SetField(ref _gpuLoadText, value);
    }

    public string GpuTemperatureText
    {
        get => _gpuTemperatureText;
        private set => SetField(ref _gpuTemperatureText, value);
    }

    public string GpuHotspotText
    {
        get => _gpuHotspotText;
        private set => SetField(ref _gpuHotspotText, value);
    }

    public string GpuCoreClockText
    {
        get => _gpuCoreClockText;
        private set => SetField(ref _gpuCoreClockText, value);
    }

    public string GpuMemoryClockText
    {
        get => _gpuMemoryClockText;
        private set => SetField(ref _gpuMemoryClockText, value);
    }

    public string GpuPowerText
    {
        get => _gpuPowerText;
        private set => SetField(ref _gpuPowerText, value);
    }

    public string GpuMemoryText
    {
        get => _gpuMemoryText;
        private set => SetField(ref _gpuMemoryText, value);
    }
    
    public ObservableCollection<RunSummaryViewModel> Runs
    {
        get;
    } = [];

    public RunSummaryViewModel? SelectedRun
    {
        get => _selectedRun;
        set
        {
            if (!SetField(ref _selectedRun, value))
            {
                return;
            }

            SelectedRunName =
                value?.Name ?? string.Empty;

            ExportStatusText = string.Empty;
            HistoryActionStatusText = string.Empty;

            RefreshExportCommands();
            RefreshHistoryCommands();
            
            _ = LoadSelectedRunAnalysisAsync();
        }
    }
    
    public RunAnalysisViewModel? SelectedRunAnalysis
    {
        get => _selectedRunAnalysis;
        private set =>
            SetField(
                ref _selectedRunAnalysis,
                value);
    }

    public string AnalysisStatusText
    {
        get => _analysisStatusText;
        private set =>
            SetField(
                ref _analysisStatusText,
                value);
    }

    public bool IsAnalysisLoading
    {
        get => _isAnalysisLoading;
        private set =>
            SetField(
                ref _isAnalysisLoading,
                value);
    }

    public bool IsRunSelectionLoading
    {
        get => _isRunSelectionLoading;
        private set =>
            SetField(
                ref _isRunSelectionLoading,
                value);
    }
    
    public string SelectedRunName
    {
        get => _selectedRunName;
        set
        {
            if (SetField(ref _selectedRunName, value))
            {
                RefreshHistoryCommands();
            }
        }
    }

    public string HistoryActionStatusText
    {
        get => _historyActionStatusText;
        private set =>
            SetField(
                ref _historyActionStatusText,
                value);
    }

    public bool IsHistoryActionRunning
    {
        get => _isHistoryActionRunning;
        private set
        {
            if (SetField(
                    ref _isHistoryActionRunning,
                    value))
            {
                RefreshHistoryCommands();
            }
        }
    }
    
    public string ExportStatusText
    {
        get => _exportStatusText;
        private set =>
            SetField(ref _exportStatusText, value);
    }

    public bool IsExporting
    {
        get => _isExporting;
        private set
        {
            if (SetField(ref _isExporting, value))
            {
                RefreshExportCommands();
            }
        }
    }

    public string HistoryStatusText
    {
        get => _historyStatusText;
        private set =>
            SetField(ref _historyStatusText, value);
    }

    public bool IsHistoryLoading
    {
        get => _isHistoryLoading;
        private set =>
            SetField(ref _isHistoryLoading, value);
    }
    
    public string GatewayAddressText
    {
        get => _gatewayAddressText;
        private set =>
            SetField(ref _gatewayAddressText, value);
    }

    public string GatewayLatencyText
    {
        get => _gatewayLatencyText;
        private set =>
            SetField(ref _gatewayLatencyText, value);
    }

    public string InternetLatencyText
    {
        get => _internetLatencyText;
        private set =>
            SetField(ref _internetLatencyText, value);
    }

    public string NetworkStatusText
    {
        get => _networkStatusText;
        private set =>
            SetField(ref _networkStatusText, value);
    }
    
    public string GatewayPacketLossText
    {
        get => _gatewayPacketLossText;
        private set =>
            SetField(
                ref _gatewayPacketLossText,
                value);
    }

    public string GatewayJitterText
    {
        get => _gatewayJitterText;
        private set =>
            SetField(
                ref _gatewayJitterText,
                value);
    }

    public string InternetPacketLossText
    {
        get => _internetPacketLossText;
        private set =>
            SetField(
                ref _internetPacketLossText,
                value);
    }

    public string InternetJitterText
    {
        get => _internetJitterText;
        private set =>
            SetField(
                ref _internetJitterText,
                value);
    }

    public string NetworkFailureText
    {
        get => _networkFailureText;
        private set =>
            SetField(
                ref _networkFailureText,
                value);
    }

    public async ValueTask DisposeAsync()
    {
        _durationTimer.Stop();
        _durationTimer.Tick -= OnDurationTimerTick;

        _monitoringSessionService.SampleCollected -=
            OnSampleCollected;

        if (_monitoringSessionService
            is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
    }

    private async void StartRun()
    {
        IsBusy = true;
        StatusText = "Run wird gestartet …";

        try
        {
            MonitoringRun run =
                await _monitoringSessionService.StartAsync(
                    RunName);

            RunName = run.Name;
            StatusText = "Run läuft";

            StartedAtText = FormatDateTime(
                run.StartedAt);

            EndedAtText = "–";
            DurationText = "00:00:00";

            CpuUsageText = "Wird gemessen …";
            MemoryUsageText = "Wird gemessen …";
            MemoryDetailsText = "–";
            LastMeasurementText = "–";
            
            GatewayAddressText = "Wird ermittelt …";
            GatewayLatencyText = "Wird gemessen …";
            InternetLatencyText = "Wird gemessen …";
            NetworkStatusText = "Wird geprüft …";
            
            GatewayPacketLossText = "Wird gemessen …";
            GatewayJitterText = "Wird gemessen …";

            InternetPacketLossText = "Wird gemessen …";
            InternetJitterText = "Wird gemessen …";

            NetworkFailureText = "Keine";
            
            LiveDisks.Clear();

            CurrentFpsText = "Wird gemessen …";
            CurrentFrameTimeText = "Wird gemessen …";
            FrameCaptureStatusText =
                "PresentMon zeichnet auf. FPS-Auswertung wird beim Stoppen übernommen.";

            IsRunning = true;

            _durationTimer.Start();
        }
        catch (Exception exception)
        {
            StatusText = $"Fehler: {exception.Message}";
            IsRunning = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void StopRun()
    {
        IsBusy = true;
        StatusText = "Run wird gestoppt …";

        try
        {
            MonitoringRun run =
                await _monitoringSessionService.StopAsync();

            _durationTimer.Stop();

            StatusText = "Run abgeschlossen";
            EndedAtText = run.EndedAt is null
                ? "–"
                : FormatDateTime(run.EndedAt.Value);

            DurationText = FormatDuration(run.Duration);
            IsRunning = false;
            await LoadRunHistoryAsync();
        }
        catch (Exception exception)
        {
            StatusText = $"Fehler: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async void ExportSelectedRunCsv()
    {
        RunSummaryViewModel? selectedRun =
            SelectedRun;

        if (selectedRun is null)
        {
            return;
        }

        string fileName =
            CreateExportFileName(
                selectedRun.Name,
                ".csv");

        string? filePath =
            _fileSaveDialogService.ShowSaveDialog(
                title: "Run als CSV exportieren",
                suggestedFileName: fileName,
                filter:
                    "CSV-Dateien (*.csv)|*.csv|" +
                    "Alle Dateien (*.*)|*.*",
                defaultExtension: ".csv");

        if (filePath is null)
        {
            return;
        }

        IsExporting = true;
        ExportStatusText =
            "CSV-Export läuft …";

        try
        {
            await _runExportService.ExportCsvAsync(
                selectedRun.Id,
                filePath);

            ExportStatusText =
                $"CSV erfolgreich gespeichert: {filePath}";
        }
        catch (Exception exception)
        {
            ExportStatusText =
                $"CSV-Export fehlgeschlagen: " +
                exception.Message;
        }
        finally
        {
            IsExporting = false;
        }
    }

    private async void ExportSelectedRunJson()
    {
        RunSummaryViewModel? selectedRun =
            SelectedRun;

        if (selectedRun is null)
        {
            return;
        }

        string fileName =
            CreateExportFileName(
                selectedRun.Name,
                ".json");

        string? filePath =
            _fileSaveDialogService.ShowSaveDialog(
                title: "Run als JSON exportieren",
                suggestedFileName: fileName,
                filter:
                    "JSON-Dateien (*.json)|*.json|" +
                    "Alle Dateien (*.*)|*.*",
                defaultExtension: ".json");

        if (filePath is null)
        {
            return;
        }

        IsExporting = true;
        ExportStatusText =
            "JSON-Export läuft …";

        try
        {
            await _runExportService.ExportJsonAsync(
                selectedRun.Id,
                filePath);

            ExportStatusText =
                $"JSON erfolgreich gespeichert: {filePath}";
        }
        catch (Exception exception)
        {
            ExportStatusText =
                $"JSON-Export fehlgeschlagen: " +
                exception.Message;
        }
        finally
        {
            IsExporting = false;
        }
    }
    
    private async void RenameSelectedRun()
    {
        RunSummaryViewModel? selectedRun =
            SelectedRun;

        string newName =
            SelectedRunName.Trim();

        if (selectedRun is null ||
            string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        IsHistoryActionRunning = true;
        HistoryActionStatusText =
            "Run wird umbenannt …";

        try
        {
            await _runHistoryService.RenameRunAsync(
                selectedRun.Id,
                newName);

            Guid selectedId =
                selectedRun.Id;

            await LoadRunHistoryAsync();

            SelectedRun =
                Runs.FirstOrDefault(
                    run => run.Id == selectedId);

            HistoryActionStatusText =
                "Run wurde erfolgreich umbenannt.";
        }
        catch (Exception exception)
        {
            HistoryActionStatusText =
                $"Umbenennen fehlgeschlagen: " +
                exception.Message;
        }
        finally
        {
            IsHistoryActionRunning = false;
        }
    }

    private async void DeleteSelectedRun()
    {
        RunSummaryViewModel? selectedRun =
            SelectedRun;

        if (selectedRun is null)
        {
            return;
        }

        bool confirmed =
            _confirmationDialogService.Confirm(
                "Run löschen",
                $"Soll der Run „{selectedRun.Name}“ " +
                "wirklich gelöscht werden?\n\n" +
                "Alle zugehörigen Messwerte werden ebenfalls gelöscht.");

        if (!confirmed)
        {
            return;
        }

        IsHistoryActionRunning = true;
        HistoryActionStatusText =
            "Run wird gelöscht …";

        try
        {
            await _runHistoryService.DeleteRunAsync(
                selectedRun.Id);

            SelectedRun = null;

            await LoadRunHistoryAsync();

            HistoryActionStatusText =
                "Run wurde erfolgreich gelöscht.";
        }
        catch (Exception exception)
        {
            HistoryActionStatusText =
                $"Löschen fehlgeschlagen: " +
                exception.Message;
        }
        finally
        {
            IsHistoryActionRunning = false;
        }
    }

    private bool CanModifySelectedRun()
    {
        return
            SelectedRun is not null &&
            !IsHistoryActionRunning &&
            !string.IsNullOrWhiteSpace(
                SelectedRunName);
    }
    
    private bool CanRenameSelectedRun()
    {
        return
            SelectedRun is not null &&
            !IsHistoryActionRunning &&
            !string.IsNullOrWhiteSpace(
                SelectedRunName);
    }

    private bool CanDeleteSelectedRun()
    {
        return
            SelectedRun is not null &&
            !IsHistoryActionRunning;
    }

    private void RefreshHistoryCommands()
    {
        _renameRunCommand.RaiseCanExecuteChanged();
        _deleteRunCommand.RaiseCanExecuteChanged();
    }

    private bool CanExportSelectedRun()
    {
        return
            SelectedRun is not null &&
            !IsExporting;
    }

    private void RefreshExportCommands()
    {
        _exportCsvCommand.RaiseCanExecuteChanged();
        _exportJsonCommand.RaiseCanExecuteChanged();
    }

    private static string CreateExportFileName(
        string runName,
        string extension)
    {
        char[] invalidCharacters =
            Path.GetInvalidFileNameChars();

        string safeName =
            new(
                runName
                    .Select(character =>
                        invalidCharacters.Contains(character)
                            ? '_'
                            : character)
                    .ToArray());

        safeName = safeName.Trim();

        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "Run";
        }

        return safeName + extension;
    }

    private void OnSampleCollected(
        object? sender,
        IMetricSample sample)
    {
        // Prozess-Samples werden bereits vom
        // MonitoringSessionService an SQLite weitergegeben.
        // Solange keine Live-Prozessansicht existiert,
        // braucht WPF dafür kein UI-Update.
        if (sample is ProcessMetricsSample or
            CpuCoreMetricsSample)
        {
            return;
        }

        WpfApplication.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (sample)
            {
                case SystemMetricsSample systemSample:
                    ApplySystemMetricsSample(
                        systemSample);
                    break;

                case NetworkMetricsSample networkSample:
                    ApplyNetworkMetricsSample(
                        networkSample);
                    break;

                case HardwareMetricsSample hardwareSample:
                    ApplyHardwareMetricsSample(
                        hardwareSample);
                    break;

                case DiskMetricsSample diskSample:
                    ApplyDiskMetricsSample(
                        diskSample);
                    break;

                case FrameMetricsSample frameSample:
                    ApplyFrameMetricsSample(frameSample);
                    break;
            }
        });
    }
    
    private void ApplyFrameMetricsSample(
        FrameMetricsSample sample)
    {
        CurrentFpsText =
            $"{sample.FramesPerSecond:F1} FPS";

        CurrentFrameTimeText =
            $"{sample.FrameTimeMilliseconds:F2} ms";

        FrameCaptureStatusText =
            $"{sample.Application} ({sample.ProcessId})";
    }

    private void ApplySystemMetricsSample(
        SystemMetricsSample sample)
    {
        CpuUsageText =
            $"{sample.CpuUsagePercent:F1} %";

        MemoryUsageText =
            $"{sample.MemoryUsagePercent:F1} %";

        MemoryDetailsText =
            $"{FormatBytes(sample.UsedMemoryBytes)} / " +
            $"{FormatBytes(sample.TotalMemoryBytes)}";

        LastMeasurementText =
            sample.Timestamp.LocalDateTime
                .ToString("HH:mm:ss");
    }

    private void ApplyNetworkMetricsSample(
        NetworkMetricsSample sample)
    {
        GatewayAddressText =
            sample.GatewayAddress ?? "Nicht gefunden";

        GatewayLatencyText =
            FormatLatency(
                sample.GatewaySuccess,
                sample.GatewayLatencyMilliseconds);

        GatewayPacketLossText =
            $"{sample.GatewayPacketLossPercent:F1} %";

        GatewayJitterText =
            FormatJitter(
                sample.GatewayJitterMilliseconds);

        InternetLatencyText =
            FormatLatency(
                sample.InternetSuccess,
                sample.InternetLatencyMilliseconds);

        InternetPacketLossText =
            $"{sample.InternetPacketLossPercent:F1} %";

        InternetJitterText =
            FormatJitter(
                sample.InternetJitterMilliseconds);

        NetworkFailureText =
            CreateFailureText(sample);

        NetworkStatusText =
            CreateNetworkStatusText(sample);
    }
    
    private static string FormatLatency(
        bool success,
        long? latencyMilliseconds)
    {
        return success &&
               latencyMilliseconds is not null
            ? $"{latencyMilliseconds.Value} ms"
            : "Timeout";
    }

    private static string FormatJitter(
        double? jitterMilliseconds)
    {
        return jitterMilliseconds is null
            ? "Noch nicht verfügbar"
            : $"{jitterMilliseconds.Value:F1} ms";
    }

    private static string CreateFailureText(
        NetworkMetricsSample sample)
    {
        if (sample.GatewayConsecutiveFailures > 0)
        {
            return
                $"{sample.GatewayConsecutiveFailures} " +
                "Gateway-Fehlschlag/-Fehlschläge in Folge";
        }

        if (sample.InternetConsecutiveFailures > 0)
        {
            return
                $"{sample.InternetConsecutiveFailures} " +
                "Internet-Fehlschlag/-Fehlschläge in Folge";
        }

        return "Keine";
    }

    private static string CreateNetworkStatusText(
        NetworkMetricsSample sample)
    {
        if (sample.GatewayConsecutiveFailures >= 3)
        {
            return
                "Lokale Netzwerkverbindung ist mehrfach " +
                "hintereinander ausgefallen";
        }

        if (!sample.GatewaySuccess)
        {
            return
                "Kurzzeitiger Ausfall zum Standardgateway erkannt";
        }

        if (sample.InternetConsecutiveFailures >= 3)
        {
            return
                "Gateway erreichbar, aber Internetverbindung " +
                "ist mehrfach hintereinander ausgefallen";
        }

        if (!sample.InternetSuccess)
        {
            return
                "Kurzzeitiger Internet-Ausfall erkannt";
        }

        if (sample.InternetPacketLossPercent >= 5)
        {
            return
                "Erhöhter Paketverlust zum Internetziel erkannt";
        }

        if (sample.InternetJitterMilliseconds >= 20)
        {
            return
                "Starke Schwankungen der Internet-Latenz erkannt";
        }

        return "Verbindung in Ordnung";
    }

    private void OnDurationTimerTick(
        object? sender,
        EventArgs eventArgs)
    {
        MonitoringRun? run =
            _monitoringSessionService.CurrentRun;

        if (run is null || !IsRunning)
        {
            return;
        }

        DurationText = FormatDuration(run.Duration);
    }

    private void RefreshCommands()
    {
        _startRunCommand.RaiseCanExecuteChanged();
        _stopRunCommand.RaiseCanExecuteChanged();
    }

    private static string FormatDateTime(
        DateTimeOffset dateTime)
    {
        return dateTime.LocalDateTime
            .ToString("dd.MM.yyyy HH:mm:ss");
    }

    private static string FormatDuration(
        TimeSpan duration)
    {
        int totalHours = (int)duration.TotalHours;

        return
            $"{totalHours:00}:" +
            $"{duration.Minutes:00}:" +
            $"{duration.Seconds:00}";
    }

    private static string FormatBytes(ulong bytes)
    {
        const double gigabyte =
            1024d * 1024d * 1024d;

        return $"{bytes / gigabyte:F1} GB";
    }


    private async void CreateDiagnosticsPackage()
    {
        if (IsRunning)
        {
            DiagnosticsStatusText = "Ein Diagnosepaket kann erst nach dem Stoppen des Runs erstellt werden.";
            return;
        }

        try
        {
            IsBusy = true;
            DiagnosticsStatusText = "Diagnosepaket wird erstellt …";
            string path = await _diagnosticsPackageService.CreateAsync();
            DiagnosticsStatusText = "Diagnosepaket erstellt: " + path;
        }
        catch (Exception exception)
        {
            AppLog.Error("Diagnosepaket konnte nicht erstellt werden.", exception);
            DiagnosticsStatusText = "Diagnosepaket konnte nicht erstellt werden: " + exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void SaveSettings()
    {
        try { await Settings.SaveAsync(); }
        catch (Exception ex) { StatusText = "Einstellungen konnten nicht gespeichert werden: " + ex.Message; }
    }

    private async void ApplyApplicationOverride()
    {
        if (SelectedRun is null || SelectedManualMainProcess is null || SelectedManualFrameProcess is null) return;
        try
        {
            await _runAnalysisService.SaveRunApplicationOverrideAsync(
                new RunApplicationOverride(
                    SelectedRun.Id,
                    0,
                    SelectedManualMainProcess.Analysis.ProcessName,
                    SelectedManualFrameProcess.Analysis.ProcessId,
                    SelectedManualFrameProcess.Analysis.Application,
                    RememberManualApplicationMapping,
                    DateTimeOffset.UtcNow));
            ManualApplicationSelectionStatusText = "Zuordnung gespeichert.";
            OnPropertyChanged(nameof(ManualApplicationSelectionStatusText));
            await LoadSelectedRunAnalysisAsync();
        }
        catch(Exception ex)
        {
            ManualApplicationSelectionStatusText = "Zuordnung fehlgeschlagen: " + ex.Message;
            OnPropertyChanged(nameof(ManualApplicationSelectionStatusText));
        }
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
        OnPropertyChanged(propertyName);

        return true;
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
    
    private void ApplyHardwareMetricsSample(
        HardwareMetricsSample sample)
    {
        CpuNameText =
            sample.CpuName ?? "Nicht erkannt";

        CpuTemperatureText =
            FormatTemperature(
                sample.CpuTemperatureCelsius);

        CpuCoreTemperatureText =
            FormatTemperature(
                sample.CpuMaximumCoreTemperatureCelsius);

        CpuClockText =
            FormatMegahertz(
                sample.CpuAverageClockMegahertz);

        CpuPowerText =
            FormatWatts(
                sample.CpuPackagePowerWatts);

        GpuNameText =
            sample.GpuName ?? "Nicht erkannt";

        GpuLoadText =
            FormatPercent(
                sample.GpuLoadPercent);

        GpuTemperatureText =
            FormatTemperature(
                sample.GpuTemperatureCelsius);

        GpuHotspotText =
            FormatTemperature(
                sample.GpuHotspotTemperatureCelsius);

        GpuCoreClockText =
            FormatMegahertz(
                sample.GpuCoreClockMegahertz);

        GpuMemoryClockText =
            FormatMegahertz(
                sample.GpuMemoryClockMegahertz);

        GpuPowerText =
            FormatWatts(
                sample.GpuPowerWatts);

        GpuMemoryText =
            FormatGpuMemory(
                sample.GpuUsedMemoryMegabytes,
                sample.GpuTotalMemoryMegabytes);
    }

    private static string FormatTemperature(
        double? value)
    {
        return value is null
            ? "Nicht verfügbar"
            : $"{value.Value:F1} °C";
    }

    private static string FormatMegahertz(
        double? value)
    {
        if (value is null)
        {
            return "Nicht verfügbar";
        }

        return value.Value >= 1_000
            ? $"{value.Value / 1_000:F2} GHz"
            : $"{value.Value:F0} MHz";
    }

    private static string FormatWatts(
        double? value)
    {
        return value is null
            ? "Nicht verfügbar"
            : $"{value.Value:F1} W";
    }

    private static string FormatPercent(
        double? value)
    {
        return value is null
            ? "Nicht verfügbar"
            : $"{value.Value:F1} %";
    }

    private static string FormatGpuMemory(
        double? usedMegabytes,
        double? totalMegabytes)
    {
        if (usedMegabytes is null)
        {
            return "Nicht verfügbar";
        }

        if (totalMegabytes is null)
        {
            return $"{usedMegabytes.Value / 1024:F1} GB verwendet";
        }

        return
            $"{usedMegabytes.Value / 1024:F1} / " +
            $"{totalMegabytes.Value / 1024:F1} GB";
    }
    
    private async Task LoadRunHistoryAsync()
    {
        if (IsHistoryLoading)
        {
            return;
        }

        IsHistoryLoading = true;
        HistoryStatusText =
            "Run-Historie wird geladen …";

        try
        {
            IReadOnlyList<RunSummary> runs =
                await _runHistoryService.GetRunsAsync();

            Runs.Clear();

            foreach (RunSummary run in runs)
            {
                Runs.Add(
                    new RunSummaryViewModel(run));
            }

            HistoryStatusText =
                runs.Count == 0
                    ? "Noch keine gespeicherten Runs vorhanden."
                    : $"{runs.Count} gespeicherte Runs";

            if (SelectedRun is not null)
            {
                SelectedRun = Runs.FirstOrDefault(
                    run => run.Id == SelectedRun.Id);
            }

            SelectedRun ??= Runs.FirstOrDefault();
        }
        catch (Exception exception)
        {
            HistoryStatusText =
                $"Run-Historie konnte nicht geladen werden: " +
                exception.Message;
        }
        finally
        {
            IsHistoryLoading = false;
        }
    }
    
    private async Task LoadSelectedRunAnalysisAsync()
    {
        int loadVersion =
            Interlocked.Increment(ref _analysisLoadVersion);

        RunSummaryViewModel? selectedRun =
            SelectedRun;

        if (selectedRun is null)
        {
            SelectedRunAnalysis = null;

            SelectedRunNetworkIncidents.Clear();
            SelectedRunDiskAnalyses.Clear();
            SelectedRunProcessAnalyses.Clear();
            SelectedRunCpuCoreAnalyses.Clear();
            SelectedRunFrameAnalyses.Clear();
            SelectedRunFrameChartPoints.Clear();
            SelectedRunStutterEpisodes.Clear();
            SelectedRunBottleneckDistribution.Clear();
            StutterSummaryText =
                "Keine Stutter-Analyse geladen.";
            SelectedRunBottleneck = null;
            SelectedRunPerformanceScore = null;
            SelectedRunRecommendations.Clear();
            SelectedRunTimelineStories.Clear();
            SelectedRunHeatmap.Clear();
            SelectedRunRegression = null;
            SelectedGameSessionStatistics = null;
            SelectedGameTrends.Clear();
            SelectedGameProfileText = "–";
            SelectedRunComparison = null;
            SelectedRunDiagnosticFindings.Clear();

            DominantProcessText = "–";
            MostLoadedCpuThreadText = "–";
            CpuThreadAssessmentText = "–";

            DiagnosticSummaryText = "–";
            DiagnosticStatusText = "Kein Run ausgewählt.";
            
            SelectedRunSampleCounts = null;
            
            SelectedRunTimelineEvents.Clear();
            TimelineStatusText = "Kein Run ausgewählt.";
            
            SelectedRunTimelinePeriods.Clear();
            TimelineStatusText = "Kein Run ausgewählt.";
            TimelineSummaryText = "–";

            AnalysisStatusText =
                "Kein Run ausgewählt.";

            if (loadVersion == _analysisLoadVersion)
            {
                IsAnalysisLoading = false;
                IsRunSelectionLoading = false;
            }

            return;
        }

        bool showRunSelectionLoading =
            _hasStartedInitialAnalysisLoad;
        _hasStartedInitialAnalysisLoad = true;

        IsAnalysisLoading = true;
        IsRunSelectionLoading = showRunSelectionLoading;
        long loadingStartedAt = Environment.TickCount64;

        if (showRunSelectionLoading)
        {
            // Ein kurzer echter Dispatcher-Leerlauf stellt sicher, dass WPF
            // das Overlay zeichnet, bevor lokale Analysearbeit den UI-Thread
            // beansprucht.
            await Task.Delay(75);
            await Dispatcher.Yield(DispatcherPriority.Background);
        }

        AnalysisStatusText =
            "Run-Auswertung wird geladen …";

        DiagnosticStatusText =
            "Diagnose wird erstellt …";

        try
        {
            Guid selectedRunId =
                selectedRun.Id;

            RunAnalysis? analysis =
                await _runAnalysisService
                    .GetRunAnalysisAsync(
                        selectedRunId);
            
            RunSampleCounts? sampleCounts =
                await _runAnalysisService
                    .GetRunSampleCountsAsync(
                        selectedRunId);

            IReadOnlyList<NetworkIncident> incidents =
                await _runAnalysisService
                    .GetNetworkIncidentsAsync(
                        selectedRunId);

            IReadOnlyList<DiskRunAnalysis> diskAnalyses =
                await _runAnalysisService
                    .GetDiskRunAnalysesAsync(
                        selectedRunId);

            IReadOnlyList<ProcessRunAnalysis> processAnalyses =
                await _runAnalysisService
                    .GetProcessRunAnalysesAsync(
                        selectedRunId);

            IReadOnlyList<CpuCoreRunAnalysis> cpuCoreAnalyses =
                await _runAnalysisService
                    .GetCpuCoreRunAnalysesAsync(
                        selectedRunId);

            IReadOnlyList<FrameRunAnalysis> frameAnalyses =
                await _runAnalysisService
                    .GetFrameRunAnalysesAsync(
                        selectedRunId);
            
            IReadOnlyList<FrameChartPoint> frameChartPoints =
                await _runAnalysisService
                    .GetFrameChartPointsAsync(
                        selectedRunId,
                        maximumPointCount: 1_500);
            
            IReadOnlyList<RunTimelineEvent> timelineEvents =
                await _runAnalysisService
                    .GetRunTimelineEventsAsync(
                        selectedRunId,
                        maximumEventCount: 5_000);

            // Der Benutzer könnte während des Ladens
            // bereits einen anderen Run ausgewählt haben.
            if (SelectedRun?.Id != selectedRunId)
            {
                return;
            }
            
            ApplyRunAnalysis(
                analysis);

            SelectedRunSampleCounts =
                sampleCounts is null
                    ? null
                    : new RunSampleCountsViewModel(
                        sampleCounts);

            ApplyNetworkIncidents(
                incidents);

            ApplyDiskAnalyses(
                diskAnalyses);

            ApplyProcessAnalyses(
                processAnalyses);

            ApplyCpuCoreAnalyses(
                cpuCoreAnalyses,
                analysis);

            SelectedRunFrameAnalyses.Clear();

            foreach (FrameRunAnalysis frameAnalysis
                     in frameAnalyses)
            {
                SelectedRunFrameAnalyses.Add(
                    new FrameRunAnalysisViewModel(frameAnalysis));
            }

            SelectedRunFrameChartPoints.Clear();

            foreach (FrameChartPoint point
                     in frameChartPoints)
            {
                SelectedRunFrameChartPoints.Add(
                    point);
            }

            StutterAnalysisResult stutterAnalysis =
                _performanceAnalysisService
                    .AnalyzeStutters(
                        selectedRunId,
                        frameChartPoints);

            SelectedRunStutterEpisodes.Clear();

            foreach (StutterEpisode episode
                     in stutterAnalysis.Episodes)
            {
                SelectedRunStutterEpisodes.Add(
                    new StutterEpisodeViewModel(
                        episode));
            }

            SelectedRunBottleneckDistribution.Clear();

            foreach (BottleneckDistributionItem item
                     in stutterAnalysis.Distribution)
            {
                SelectedRunBottleneckDistribution.Add(
                    new BottleneckDistributionItemViewModel(
                        item));
            }

            StutterSummaryText =
                stutterAnalysis.Episodes.Count == 0
                    ? "Keine zusammenhängenden Stutter-Episoden erkannt."
                    : $"{stutterAnalysis.Episodes.Count} Episode(n), " +
                      $"{stutterAnalysis.TotalSpikeCount} Spike(s), " +
                      $"{stutterAnalysis.TotalStutterDurationSeconds:F2} s " +
                      "gewichtete Stutter-Dauer.";

            SelectedRunAnalysisQuality =
                _analysisQualityService.Evaluate(
                    selectedRun.Run,
                    sampleCounts,
                    frameChartPoints,
                    frameAnalyses);
            OnPropertyChanged(nameof(SelectedRunAnalysisQuality));
            OnPropertyChanged(nameof(AnalysisVersionText));

            BottleneckAnalysis bottleneck =
                _performanceAnalysisService
                    .AnalyzeBottleneck(
                        selectedRunId,
                        analysis,
                        diskAnalyses,
                        processAnalyses,
                        cpuCoreAnalyses,
                        frameAnalyses,
                        incidents);

            SelectedRunBottleneck =
                new BottleneckAnalysisViewModel(
                    bottleneck);

            string gameName =
                frameAnalyses
                    .FirstOrDefault(item => item.IsLikelyGame)?
                    .MatchedToProcessName
                ?? frameAnalyses
                    .FirstOrDefault(item => item.IsLikelyGame)?
                    .Application
                ?? selectedRun.Name;

            IReadOnlyList<RunPerformanceSnapshot>
                historicalSnapshots =
                    await LoadHistoricalPerformanceSnapshotsAsync(
                        selectedRunId,
                        gameName);

            FrameRunAnalysis? selectedGameFrame =
                frameAnalyses.FirstOrDefault(item =>
                    item.IsLikelyGame)
                ?? frameAnalyses.FirstOrDefault();

            AdvancedPerformanceAnalysis advanced =
                _performanceAnalysisService
                    .AnalyzeAdvanced(
                        selectedRun.Run,
                        gameName,
                        analysis,
                        selectedGameFrame,
                        frameChartPoints,
                        bottleneck,
                        stutterAnalysis,
                        historicalSnapshots);

            ApplyAdvancedPerformanceAnalysis(
                advanced);

            await _runAnalysisService.SavePersistedAnalysisResultAsync(
                new PersistedAnalysisResult(
                    selectedRunId,
                    AnalysisVersionInfo.AnalysisVersion,
                    AnalysisVersionInfo.RuleSetVersion,
                    DateTimeOffset.UtcNow,
                    advanced.Profile.Id,
                    gameName,
                    selectedGameFrame?.MatchedToProcessId,
                    selectedGameFrame?.Application ?? string.Empty,
                    selectedGameFrame?.ProcessId,
                    SelectedRunAnalysisQuality!,
                    advanced.Score,
                    bottleneck,
                    advanced.Recommendations));

            ApplyDiagnosticFindings(
                analysis,
                incidents,
                diskAnalyses,
                processAnalyses,
                cpuCoreAnalyses,
                frameAnalyses,
                bottleneck);

            AnalysisStatusText =
                analysis is null
                    ? "Für diesen Run wurde keine Hauptauswertung gefunden."
                    : "Auswertung geladen.";
            
            SelectedRunTimelineEvents.Clear();

            foreach (RunTimelineEvent timelineEvent
                     in timelineEvents)
            {
                SelectedRunTimelineEvents.Add(
                    new RunTimelineEventViewModel(
                        timelineEvent));
            }

            TimelineStatusText =
                timelineEvents.Count switch
                {
                    0 =>
                        "Keine auffälligen Zeitpunkte erkannt.",

                    1 =>
                        "1 auffälliger Zeitpunkt erkannt.",

                    _ =>
                        $"{timelineEvents.Count} auffällige Zeitpunkte erkannt."
                };
            
            IReadOnlyList<RunTimelinePeriod> timelinePeriods =
                _runTimelineService.CreatePeriods(
                    timelineEvents);

            SelectedRunTimelinePeriods.Clear();

            foreach (RunTimelinePeriod period
                     in timelinePeriods)
            {
                SelectedRunTimelinePeriods.Add(
                    new RunTimelinePeriodViewModel(
                        period));
            }

            int criticalPeriods =
                timelinePeriods.Count(period =>
                    period.Severity ==
                    DiagnosticSeverity.Critical);

            int warningPeriods =
                timelinePeriods.Count(period =>
                    period.Severity ==
                    DiagnosticSeverity.Warning);

            int informationPeriods =
                timelinePeriods.Count(period =>
                    period.Severity ==
                    DiagnosticSeverity.Information);

            int correlationPeriods =
                timelinePeriods.Count(period =>
                    period.IsCorrelation);

            TimelineStatusText =
                timelinePeriods.Count switch
                {
                    0 =>
                        "Keine auffälligen Zeiträume erkannt.",

                    1 =>
                        correlationPeriods == 1
                            ? "1 korrelierter Auffälligkeitszeitraum erkannt."
                            : "1 auffälliger Zeitraum erkannt.",

                    _ =>
                        $"{timelinePeriods.Count} auffällige Zeiträume erkannt, " +
                        $"davon {correlationPeriods} Korrelation(en)."
                };

            TimelineSummaryText =
                $"{criticalPeriods} kritisch, " +
                $"{warningPeriods} Warnung(en), " +
                $"{informationPeriods} Hinweis(e), " +
                $"{correlationPeriods} Korrelation(en)";

            await LoadRunComparisonAsync();
        }
        catch (Exception exception)
        {
            if (SelectedRun?.Id != selectedRun.Id)
            {
                return;
            }

            SelectedRunAnalysis = null;

            SelectedRunNetworkIncidents.Clear();
            SelectedRunDiskAnalyses.Clear();
            SelectedRunProcessAnalyses.Clear();
            SelectedRunCpuCoreAnalyses.Clear();
            SelectedRunFrameAnalyses.Clear();
            SelectedRunFrameChartPoints.Clear();
            SelectedRunStutterEpisodes.Clear();
            SelectedRunBottleneckDistribution.Clear();
            StutterSummaryText =
                "Keine Stutter-Analyse geladen.";
            SelectedRunBottleneck = null;
            SelectedRunPerformanceScore = null;
            SelectedRunRecommendations.Clear();
            SelectedRunTimelineStories.Clear();
            SelectedRunHeatmap.Clear();
            SelectedRunRegression = null;
            SelectedGameSessionStatistics = null;
            SelectedGameTrends.Clear();
            SelectedGameProfileText = "–";
            SelectedRunComparison = null;
            SelectedRunDiagnosticFindings.Clear();

            DominantProcessText = "–";
            MostLoadedCpuThreadText = "–";
            CpuThreadAssessmentText = "–";

            DiagnosticSummaryText = "–";
            DiagnosticStatusText =
                "Diagnose konnte nicht erstellt werden.";
            
            SelectedRunSampleCounts = null;
            
            SelectedRunTimelineEvents.Clear();
            TimelineStatusText =
                "Timeline konnte nicht geladen werden.";
            
            SelectedRunTimelinePeriods.Clear();
            TimelineStatusText =
                "Timeline konnte nicht geladen werden.";
            TimelineSummaryText = "–";

            AnalysisStatusText =
                "Auswertung konnte nicht geladen werden: " +
                exception.Message;
        }
        finally
        {
            if (loadVersion == _analysisLoadVersion)
            {
                int minimumLoadingDisplayMilliseconds =
                    showRunSelectionLoading ? 550 : 0;
                long elapsedMilliseconds =
                    Environment.TickCount64 - loadingStartedAt;
                int remainingMilliseconds =
                    minimumLoadingDisplayMilliseconds -
                    (int)Math.Min(
                        elapsedMilliseconds,
                        minimumLoadingDisplayMilliseconds);

                if (remainingMilliseconds > 0)
                {
                    await Task.Delay(remainingMilliseconds);
                }

                if (loadVersion == _analysisLoadVersion)
                {
                    IsAnalysisLoading = false;
                    IsRunSelectionLoading = false;
                }
            }
        }
    }
    
    private void ApplyDiskMetricsSample(
        DiskMetricsSample sample)
    {
        DiskLiveViewModel? existingDisk =
            LiveDisks.FirstOrDefault(
                disk => string.Equals(
                    disk.DeviceId,
                    sample.DeviceId,
                    StringComparison.Ordinal));

        if (existingDisk is null)
        {
            existingDisk =
                new DiskLiveViewModel(
                    sample.DeviceId);

            LiveDisks.Add(existingDisk);
        }

        existingDisk.Apply(sample);
    }
    
    private void ApplyCpuCoreAnalysisSummary(
        IReadOnlyList<CpuCoreRunAnalysis> coreAnalyses,
        RunAnalysis? runAnalysis)
    {
        if (coreAnalyses.Count == 0)
        {
            MostLoadedCpuThreadText =
                "Keine CPU-Thread-Daten verfügbar.";

            CpuThreadAssessmentText =
                "Für diesen Run wurden keine einzelnen " +
                "CPU-Threads erfasst.";

            return;
        }

        CpuCoreRunAnalysis mostLoadedThread =
            coreAnalyses
                .OrderByDescending(core =>
                    core.ShareAbove95Percent)
                .ThenByDescending(core =>
                    core.ShareAbove90Percent)
                .ThenByDescending(core =>
                    core.MaximumLoadPercent)
                .ThenByDescending(core =>
                    core.AverageLoadPercent)
                .First();

        MostLoadedCpuThreadText =
            $"Core {mostLoadedThread.CoreIndex}, " +
            $"Thread {mostLoadedThread.ThreadIndex}: " +
            $"{mostLoadedThread.AverageLoadPercent:F1} % Ø, " +
            $"{mostLoadedThread.MaximumLoadPercent:F1} % max., " +
            $"{mostLoadedThread.ShareAbove90Percent:F1} % " +
            "der Messungen über 90 %.";

        CpuThreadAssessmentText =
            CreateCpuThreadAssessment(
                coreAnalyses,
                runAnalysis);
    }
    
    private static string CreateCpuThreadAssessment(
        IReadOnlyList<CpuCoreRunAnalysis> coreAnalyses,
        RunAnalysis? runAnalysis)
    {
        CpuCoreRunAnalysis? sustainedCriticalThread =
            coreAnalyses
                .Where(core =>
                    core.ShareAbove95Percent >= 20)
                .OrderByDescending(core =>
                    core.ShareAbove95Percent)
                .FirstOrDefault();

        if (sustainedCriticalThread is not null)
        {
            return
                $"Core {sustainedCriticalThread.CoreIndex}, " +
                $"Thread {sustainedCriticalThread.ThreadIndex} " +
                "war über einen längeren Zeitraum nahezu " +
                "vollständig ausgelastet. Das kann auf ein " +
                "CPU- oder Main-Thread-Limit hindeuten.";
        }

        CpuCoreRunAnalysis? frequentlyLoadedThread =
            coreAnalyses
                .Where(core =>
                    core.ShareAbove90Percent >= 10)
                .OrderByDescending(core =>
                    core.ShareAbove90Percent)
                .FirstOrDefault();

        if (frequentlyLoadedThread is not null)
        {
            return
                $"Core {frequentlyLoadedThread.CoreIndex}, " +
                $"Thread {frequentlyLoadedThread.ThreadIndex} " +
                "war häufig über 90 % ausgelastet. " +
                "Die CPU-Gesamtauslastung kann dabei trotzdem " +
                "vergleichsweise niedrig aussehen.";
        }

        bool hasShortCriticalPeak =
            coreAnalyses.Any(core =>
                core.MaximumLoadPercent >= 95);

        if (hasShortCriticalPeak)
        {
            return
                "Mindestens ein CPU-Thread erreichte kurzzeitig " +
                "eine sehr hohe Auslastung, jedoch nicht lange " +
                "genug für einen eindeutigen CPU-Engpass.";
        }

        if (runAnalysis?.MaximumGpuLoadPercent >= 97)
        {
            return
                "Kein einzelner CPU-Thread war über längere Zeit " +
                "stark ausgelastet, während die GPU nahezu voll " +
                "ausgelastet war. Das spricht eher für ein GPU-Limit.";
        }

        return
            "In den einzelnen CPU-Threads wurde keine länger " +
            "anhaltende kritische Auslastung erkannt.";
    }
    
    public string DiagnosticSummaryText
    {
        get => _diagnosticSummaryText;
        private set =>
            SetField(
                ref _diagnosticSummaryText,
                value);
    }

    public string DiagnosticStatusText
    {
        get => _diagnosticStatusText;
        private set =>
            SetField(
                ref _diagnosticStatusText,
                value);
    }
    
    private void ApplyRunAnalysis(
        RunAnalysis? analysis)
    {
        SelectedRunAnalysis =
            analysis is null
                ? null
                : new RunAnalysisViewModel(
                    analysis);
    }
    
    private void ApplyNetworkIncidents(
        IReadOnlyList<NetworkIncident> incidents)
    {
        SelectedRunNetworkIncidents.Clear();

        foreach (NetworkIncident incident in incidents)
        {
            SelectedRunNetworkIncidents.Add(
                new NetworkIncidentViewModel(
                    incident));
        }
    }
    
    private void ApplyDiskAnalyses(
        IReadOnlyList<DiskRunAnalysis> analyses)
    {
        SelectedRunDiskAnalyses.Clear();

        foreach (DiskRunAnalysis analysis in analyses)
        {
            SelectedRunDiskAnalyses.Add(
                new DiskRunAnalysisViewModel(
                    analysis));
        }
    }
    
    private void ApplyProcessAnalyses(
        IReadOnlyList<ProcessRunAnalysis> analyses)
    {
        SelectedRunProcessAnalyses.Clear();

        foreach (ProcessRunAnalysis analysis
                 in analyses.Take(20))
        {
            SelectedRunProcessAnalyses.Add(
                new ProcessRunAnalysisViewModel(
                    analysis));
        }

        ProcessRunAnalysis? dominantProcess =
            analyses
                .Where(process =>
                    process.ForegroundSampleCount > 0)
                .OrderByDescending(process =>
                    process.ForegroundSampleCount)
                .ThenByDescending(process =>
                    process.AverageCpuUsagePercent)
                .FirstOrDefault();

        DominantProcessText =
            dominantProcess is null
                ? "Kein eindeutiger Vordergrundprozess erkannt."
                : $"{dominantProcess.ProcessName} – " +
                  $"{dominantProcess.ForegroundSharePercent:F1} % " +
                  "der erfassten Prozess-Samples im Vordergrund.";
    }
    
    private void ApplyCpuCoreAnalyses(
        IReadOnlyList<CpuCoreRunAnalysis> analyses,
        RunAnalysis? runAnalysis)
    {
        SelectedRunCpuCoreAnalyses.Clear();

        foreach (CpuCoreRunAnalysis analysis in analyses)
        {
            SelectedRunCpuCoreAnalyses.Add(
                new CpuCoreRunAnalysisViewModel(
                    analysis));
        }

        ApplyCpuCoreAnalysisSummary(
            analyses,
            runAnalysis);
    }
    

    private async Task LoadRunComparisonAsync()
    {
        RunSummaryViewModel? baseRun =
            SelectedRun;

        RunSummaryViewModel? comparisonRun =
            SelectedComparisonRun;

        if (baseRun is null ||
            comparisonRun is null ||
            baseRun.Id == comparisonRun.Id)
        {
            SelectedRunComparison = null;

            ComparisonStatusText =
                comparisonRun is null
                    ? "Vergleichsrun auswählen."
                    : "Bitte zwei unterschiedliche Runs auswählen.";

            return;
        }

        ComparisonStatusText =
            "Run-Vergleich wird berechnet …";

        try
        {
            RunAnalysis? baseAnalysis =
                await _runAnalysisService
                    .GetRunAnalysisAsync(
                        baseRun.Id);

            RunAnalysis? comparisonAnalysis =
                await _runAnalysisService
                    .GetRunAnalysisAsync(
                        comparisonRun.Id);

            IReadOnlyList<FrameRunAnalysis> baseFrames =
                await _runAnalysisService
                    .GetFrameRunAnalysesAsync(
                        baseRun.Id);

            IReadOnlyList<FrameRunAnalysis> comparisonFrames =
                await _runAnalysisService
                    .GetFrameRunAnalysesAsync(
                        comparisonRun.Id);

            IReadOnlyList<DiskRunAnalysis> baseDisks =
                await _runAnalysisService
                    .GetDiskRunAnalysesAsync(
                        baseRun.Id);

            IReadOnlyList<DiskRunAnalysis> comparisonDisks =
                await _runAnalysisService
                    .GetDiskRunAnalysesAsync(
                        comparisonRun.Id);

            IReadOnlyList<ProcessRunAnalysis> baseProcesses =
                await _runAnalysisService
                    .GetProcessRunAnalysesAsync(
                        baseRun.Id);

            IReadOnlyList<ProcessRunAnalysis> comparisonProcesses =
                await _runAnalysisService
                    .GetProcessRunAnalysesAsync(
                        comparisonRun.Id);

            IReadOnlyList<CpuCoreRunAnalysis> baseCores =
                await _runAnalysisService
                    .GetCpuCoreRunAnalysesAsync(
                        baseRun.Id);

            IReadOnlyList<CpuCoreRunAnalysis> comparisonCores =
                await _runAnalysisService
                    .GetCpuCoreRunAnalysesAsync(
                        comparisonRun.Id);

            IReadOnlyList<NetworkIncident> baseIncidents =
                await _runAnalysisService
                    .GetNetworkIncidentsAsync(
                        baseRun.Id);

            IReadOnlyList<NetworkIncident> comparisonIncidents =
                await _runAnalysisService
                    .GetNetworkIncidentsAsync(
                        comparisonRun.Id);

            BottleneckAnalysis baseBottleneck =
                _performanceAnalysisService
                    .AnalyzeBottleneck(
                        baseRun.Id,
                        baseAnalysis,
                        baseDisks,
                        baseProcesses,
                        baseCores,
                        baseFrames,
                        baseIncidents);

            BottleneckAnalysis comparisonBottleneck =
                _performanceAnalysisService
                    .AnalyzeBottleneck(
                        comparisonRun.Id,
                        comparisonAnalysis,
                        comparisonDisks,
                        comparisonProcesses,
                        comparisonCores,
                        comparisonFrames,
                        comparisonIncidents);

            RunComparison comparison =
                _performanceAnalysisService
                    .CreateComparison(
                        baseRun.Id,
                        baseRun.Name,
                        comparisonRun.Id,
                        comparisonRun.Name,
                        baseAnalysis,
                        comparisonAnalysis,
                        baseFrames,
                        comparisonFrames,
                        baseBottleneck,
                        comparisonBottleneck);

            if (SelectedRun?.Id != baseRun.Id ||
                SelectedComparisonRun?.Id !=
                comparisonRun.Id)
            {
                return;
            }

            SelectedRunComparison =
                new RunComparisonViewModel(
                    comparison);

            ComparisonStatusText =
                "Run-Vergleich geladen.";
        }
        catch (Exception exception)
        {
            SelectedRunComparison = null;

            ComparisonStatusText =
                "Run-Vergleich fehlgeschlagen: " +
                exception.Message;
        }
    }


    private async Task<IReadOnlyList<RunPerformanceSnapshot>>
        LoadHistoricalPerformanceSnapshotsAsync(
            Guid selectedRunId,
            string gameName)
    {
        var snapshots =
            new List<RunPerformanceSnapshot>();

        foreach (RunSummaryViewModel candidate
                 in Runs
                     .Where(item =>
                         item.Id != selectedRunId &&
                         item.Run.Status == RunStatus.Completed)
                     .OrderByDescending(item =>
                         item.Run.StartedAt)
                     .Take(20))
        {
            IReadOnlyList<FrameRunAnalysis> frames =
                await _runAnalysisService
                    .GetFrameRunAnalysesAsync(
                        candidate.Id);

            FrameRunAnalysis? gameFrame =
                frames.FirstOrDefault(item =>
                    item.IsLikelyGame)
                ?? frames.FirstOrDefault();

            string candidateGameName =
                gameFrame?.MatchedToProcessName
                ?? gameFrame?.Application
                ?? candidate.Name;

            if (!string.Equals(
                    NormalizeGameName(candidateGameName),
                    NormalizeGameName(gameName),
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            RunAnalysis? analysis =
                await _runAnalysisService
                    .GetRunAnalysisAsync(
                        candidate.Id);

            IReadOnlyList<DiskRunAnalysis> disks =
                await _runAnalysisService
                    .GetDiskRunAnalysesAsync(
                        candidate.Id);

            IReadOnlyList<ProcessRunAnalysis> processes =
                await _runAnalysisService
                    .GetProcessRunAnalysesAsync(
                        candidate.Id);

            IReadOnlyList<CpuCoreRunAnalysis> cores =
                await _runAnalysisService
                    .GetCpuCoreRunAnalysesAsync(
                        candidate.Id);

            IReadOnlyList<NetworkIncident> incidents =
                await _runAnalysisService
                    .GetNetworkIncidentsAsync(
                        candidate.Id);

            IReadOnlyList<FrameChartPoint> points =
                await _runAnalysisService
                    .GetFrameChartPointsAsync(
                        candidate.Id,
                        maximumPointCount: 1_000);

            BottleneckAnalysis bottleneck =
                _performanceAnalysisService
                    .AnalyzeBottleneck(
                        candidate.Id,
                        analysis,
                        disks,
                        processes,
                        cores,
                        frames,
                        incidents);

            StutterAnalysisResult stutters =
                _performanceAnalysisService
                    .AnalyzeStutters(
                        candidate.Id,
                        points);

            GameProfile profile =
                _performanceAnalysisService
                    .DetectGameProfile(
                        candidateGameName);

            PerformanceScoreResult score =
                _performanceAnalysisService
                    .CreatePerformanceScore(
                        candidate.Id,
                        profile,
                        analysis,
                        gameFrame,
                        bottleneck,
                        stutters);

            snapshots.Add(
                new RunPerformanceSnapshot(
                    candidate.Run,
                    candidateGameName,
                    analysis,
                    gameFrame,
                    bottleneck,
                    stutters,
                    score));
        }

        return snapshots;
    }

    private void ApplyAdvancedPerformanceAnalysis(
        AdvancedPerformanceAnalysis analysis)
    {
        SelectedGameProfileText =
            $"{analysis.Profile.DisplayName}: " +
            analysis.Profile.Description;

        SelectedRunPerformanceScore =
            new PerformanceScoreViewModel(
                analysis.Score);

        SelectedRunRecommendations.Clear();

        foreach (PerformanceRecommendation item
                 in analysis.Recommendations)
        {
            SelectedRunRecommendations.Add(
                new PerformanceRecommendationViewModel(
                    item));
        }

        SelectedRunTimelineStories.Clear();

        foreach (TimelineStory item
                 in analysis.TimelineStories)
        {
            SelectedRunTimelineStories.Add(
                new TimelineStoryViewModel(
                    item));
        }

        SelectedRunHeatmap.Clear();

        foreach (PerformanceHeatmapCell item
                 in analysis.Heatmap)
        {
            SelectedRunHeatmap.Add(
                new PerformanceHeatmapCellViewModel(
                    item));
        }

        SelectedRunRegression =
            new PerformanceRegressionViewModel(
                analysis.Regression);

        SelectedGameSessionStatistics =
            new GameSessionStatisticsViewModel(
                analysis.SessionStatistics);

        SelectedGameTrends.Clear();

        foreach (PerformanceTrendPoint item
                 in analysis.Trends)
        {
            SelectedGameTrends.Add(
                new PerformanceTrendPointViewModel(
                    item));
        }
    }

    private static string NormalizeGameName(
        string value)
    {
        return value
            .Replace(
                ".exe",
                string.Empty,
                StringComparison.OrdinalIgnoreCase)
            .Trim()
            .ToLowerInvariant();
    }

    private void ApplyDiagnosticFindings(
        RunAnalysis? analysis,
        IReadOnlyList<NetworkIncident> incidents,
        IReadOnlyList<DiskRunAnalysis> diskAnalyses,
        IReadOnlyList<ProcessRunAnalysis> processAnalyses,
        IReadOnlyList<CpuCoreRunAnalysis> cpuCoreAnalyses,
        IReadOnlyList<FrameRunAnalysis> frameAnalyses,
        BottleneckAnalysis bottleneckAnalysis)
    {
        var context =
            new RunDiagnosticContext(
                RunAnalysis: analysis,
                NetworkIncidents: incidents,
                DiskAnalyses: diskAnalyses,
                ProcessAnalyses: processAnalyses,
                CpuCoreAnalyses: cpuCoreAnalyses,
                FrameAnalyses: frameAnalyses,
                BottleneckAnalysis: bottleneckAnalysis);

        IReadOnlyList<DiagnosticFinding> findings =
            _runDiagnosticService.Analyze(
                context);

        SelectedRunDiagnosticFindings.Clear();

        foreach (DiagnosticFinding finding in findings)
        {
            SelectedRunDiagnosticFindings.Add(
                new DiagnosticFindingViewModel(
                    finding));
        }

        int criticalCount =
            findings.Count(finding =>
                finding.Severity ==
                DiagnosticSeverity.Critical);

        int warningCount =
            findings.Count(finding =>
                finding.Severity ==
                DiagnosticSeverity.Warning);

        int informationCount =
            findings.Count(finding =>
                finding.Severity ==
                DiagnosticSeverity.Information);

        DiagnosticSummaryText =
            $"{criticalCount} kritisch, " +
            $"{warningCount} Warnung(en), " +
            $"{informationCount} Hinweis(e)";

        DiagnosticStatusText =
            findings.Count == 1
                ? "1 Diagnosehinweis erzeugt."
                : $"{findings.Count} Diagnosehinweise erzeugt.";
    }
}
