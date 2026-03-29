using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Styling;
using NovaSetup.Models;
using NovaSetup.Services;

namespace NovaSetup.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private const string UnsupportedSelectionNote = "Selected from selection.json but unsupported on this OS.";
    private const string SectionDashboard = "Dashboard";
    private const string SectionApps = "Apps";
    private const string SectionDrivers = "Drivers";
    private const string SectionMyLists = "My Lists";
    private const string SectionUpdates = "Updates";
    private const string SectionHistory = "History";
    private const string SectionLogs = "Logs";
    private const string SectionAbout = "About";
    private const string NovaGitHubUrl = "https://github.com/TugyR3za/Nova-setup-v.001";
    private const string LogFilterAll = "All";
    private const string LogFilterInfo = "Info";
    private const string LogFilterSuccess = "Success";
    private const string LogFilterWarning = "Warning";
    private const string LogFilterError = "Error";
    private const string LogFilterDebug = "Debug";

    private readonly PlatformService _platformService;
    private readonly CatalogService _catalogService;
    private readonly SelectionService _selectionService;
    private readonly DetectionService _detectionService;
    private readonly InstallerService _installerService;
    private readonly LoggingService _loggingService;
    private readonly BrowserService _browserService;
    private readonly SettingsService _settingsService;
    private readonly ProfileService _profileService;
    private readonly AppPreferencesService _appPreferencesService;
    private readonly DependencyResolverService _dependencyResolverService;
    private readonly AppUpdateService _appUpdateService;
    private ScheduledUpdateService? _scheduledUpdateService;

    private bool _isGridViewActive;
    private readonly ObservableCollection<AppItem> _apps = new();
    private readonly ObservableCollection<AppItem> _visibleApps = new();
    private readonly ObservableCollection<InstallQueueItem> _installQueue = new();
    private readonly ObservableCollection<NovaProfile> _savedProfiles = new();
    private readonly ObservableCollection<LogEntry> _filteredLiveLogs = new();
    private readonly ObservableCollection<InstallResult> _installResults = new();
    private readonly ObservableCollection<InstallResult> _installedResults = new();
    private readonly ObservableCollection<InstallResult> _failedResults = new();
    private readonly ObservableCollection<InstallResult> _cancelledResults = new();
    private readonly ObservableCollection<InstallResult> _skippedResults = new();
    private readonly ObservableCollection<InstallResult> _restartRequiredResults = new();
    private readonly ObservableCollection<InstallResult> _unsupportedSkippedResults = new();

    private readonly AsyncRelayCommand _installCommand;
    private readonly RelayCommand _saveListCommand;
    private readonly RelayCommand _saveProfileCommand;
    private readonly AsyncRelayCommand _confirmSaveProfileCommand;
    private readonly RelayCommand _cancelSaveProfileCommand;
    private readonly AsyncRelayCommand _loadProfileCommand;
    private readonly AsyncRelayCommand _exportProfileCommand;
    private readonly AsyncRelayCommand _loadSavedProfileCommand;
    private readonly AsyncRelayCommand _deleteSavedProfileCommand;
    private readonly RelayCommand _openLogFileCommand;
    private readonly RelayCommand _showHelpCommand;
    private readonly AsyncRelayCommand _refreshCatalogCommand;
    private readonly RelayCommand _showUpdatesFilterCommand;
    private readonly RelayCommand _showRecommendedFilterCommand;
    private readonly RelayCommand _dismissUpdateCommand;
    private readonly RelayCommand _downloadUpdateCommand;
    private readonly AsyncRelayCommand _manualCheckForUpdatesCommand;
    private readonly AsyncRelayCommand _runScheduledUpdatesNowCommand;
    private readonly AsyncRelayCommand _clearLogsCommand;
    private readonly AsyncRelayCommand _copyLogsCommand;
    private readonly AsyncRelayCommand _copySelectedLogCommand;
    private readonly AsyncRelayCommand _exportLogsCommand;
    private readonly AsyncRelayCommand _browsePortableFolderCommand;
    private readonly RelayCommand _installAppCommand;
    private readonly RelayCommand _updateAppCommand;
    private readonly RelayCommand _uninstallCommand;
    private readonly RelayCommand _openInstallLocationCommand;
    private readonly RelayCommand _copyToClipboardCommand;
    private readonly RelayCommand _toggleSilentInstallPreferenceCommand;
    private readonly RelayCommand _toggleScanningPreferenceCommand;
    private readonly RelayCommand _toggleInstallScriptsPreferenceCommand;
    private readonly RelayCommand _openPublisherCommand;
    private readonly RelayCommand _openAboutGitHubCommand;
    private readonly RelayCommand _showAppDetailsCommand;
    private readonly RelayCommand _closeAppDetailsCommand;
    private readonly RelayCommand _pauseInstallCommand;
    private readonly RelayCommand _clearQueueCommand;
    private readonly RelayCommand _exportReportCommand;
    private readonly RelayCommand _toggleSettingsPanelCommand;
    private readonly RelayCommand _toggleAccountMenuCommand;
    private readonly RelayCommand _openAccountProfileCommand;
    private readonly RelayCommand _openAccountSettingsCommand;
    private readonly RelayCommand _navigateDashboardCommand;
    private readonly RelayCommand _navigateAppsCommand;
    private readonly RelayCommand _navigateDriversCommand;
    private readonly RelayCommand _navigateMyListsCommand;
    private readonly RelayCommand _navigateUpdatesCommand;
    private readonly RelayCommand _navigateHistoryCommand;
    private readonly RelayCommand _navigateLogsCommand;
    private readonly RelayCommand _openLogsFromSettingsCommand;
    private readonly RelayCommand _navigateAboutCommand;
    private readonly RelayCommand _requestRestartNowCommand;
    private readonly AsyncRelayCommand _confirmRestartNowCommand;
    private readonly RelayCommand _cancelRestartCommand;
    private readonly RelayCommand _restartLaterCommand;
    private readonly RelayCommand _toggleViewModeCommand;
    private readonly AsyncRelayCommand _resetSettingsCommand;

    private bool _isInitialized;
    private bool _isInitializing;
    private bool _isInstalling;
    private bool _isQueueVisible;
    private bool _restartRequired;
    private bool _isRestartConfirmationVisible;
    private bool _restartDecisionFinalized;
    private bool _isSettingsPanelOpen;
    private bool _isAccountMenuOpen;
    private bool _isAppDetailsOpen;
    private bool _isSaveProfilePanelOpen;
    private bool _suppressSettingsLogging;
    private bool _suppressAppSelectionHandling;
    private bool _installedAppsDetected;
    private bool _updatingFilterFlags;
    private string _currentPlatformId = PlatformService.Unknown;
    private string _currentPlatform = "Unknown OS";
    private string _currentProfileName = "Website Starter";
    private string _pendingProfileName = "My Setup";
    private string _pendingProfileDescription = string.Empty;
    private string _statusText = "Ready.";
    private string _installStatusText = "Install has not started.";
    private string _installSummaryText = string.Empty;
    private string _restartStatusText = string.Empty;
    private string _currentSection = SectionApps;
    private string _searchText = string.Empty;
    private string _selectedFilter = "All";
    private bool _isUpdateAvailable;
    private string _updateVersionText = string.Empty;
    private string _updateDownloadUrl = string.Empty;
    private bool _isDependencyInfoVisible;
    private string _dependencyInfoText = string.Empty;
    private string _aboutUpdateStatusText = string.Empty;
    private string _aboutRemoteVersionText = "Latest available: Not checked yet";
    private AppItem? _selectedDetailApp;
    private NovaProfile? _selectedSavedProfile;
    private LogEntry? _selectedLiveLogEntry;
    private bool _isAllFilter = true;
    private bool _isGamesFilter;
    private bool _isDriversFilter;
    private bool _isRecommendedFilter;
    private bool _isDevToolsFilter;
    private bool _isUtilitiesFilter;
    private bool _isArm64Filter;
    private bool _isUpdatesFilter;
    private string _selectedLogLevelFilter = LogFilterAll;
    private double _progressValue;
    private CancellationTokenSource? _settingsSaveCts;
    private CancellationTokenSource? _startupDetectionCts;
    private CancellationTokenSource? _dependencyInfoDismissCts;
    private CancellationTokenSource? _installQueueCts;

    private static readonly IReadOnlyList<SettingChoice> RestartBehaviorChoices =
    [
        new(AppSettings.RestartAskBeforeRestart, "Ask before restart"),
        new(AppSettings.RestartAutomatically, "Restart automatically"),
        new(AppSettings.RestartNever, "Never restart")
    ];

    private static readonly IReadOnlyList<SettingChoice> DownloadLocationChoices =
    [
        new(AppSettings.DownloadSystemDefault, "System default"),
        new(AppSettings.DownloadCustomFolder, "Custom folder"),
        new(AppSettings.DownloadAskEveryTime, "Ask every time")
    ];

    private static readonly IReadOnlyList<SettingChoice> ThemeChoices =
    [
        new(AppSettings.ThemeDark, "Dark"),
        new(AppSettings.ThemeLight, "Light"),
        new(AppSettings.ThemeSystem, "System")
    ];

    private static readonly IReadOnlyList<SettingChoice> LanguageChoices =
    [
        new(AppSettings.LanguageEnglish, "English"),
        new(AppSettings.LanguageFarsi, "Farsi"),
        new(AppSettings.LanguageTurkish, "Turkish")
    ];

    private static readonly IReadOnlyList<SettingChoice> LogLevelFilterChoices =
    [
        new(LogFilterAll, "All levels"),
        new(LogFilterInfo, "Info"),
        new(LogFilterSuccess, "Success"),
        new(LogFilterWarning, "Warning"),
        new(LogFilterError, "Error"),
        new(LogFilterDebug, "Debug")
    ];

    private static readonly IReadOnlyList<SettingChoice> ScheduledUpdateFrequencyChoices =
    [
        new(AppSettings.ScheduledFrequencyDaily, "Daily"),
        new(AppSettings.ScheduledFrequencyWeekly, "Weekly"),
        new(AppSettings.ScheduledFrequencyMonthly, "Monthly")
    ];

    private static readonly IReadOnlyList<SettingChoice> ScheduledUpdateHourChoices =
        Enumerable.Range(0, 24)
            .Select(hour => new SettingChoice(hour.ToString(), $"{hour:00}:00"))
            .ToList();

    private static readonly IReadOnlyList<SettingChoice> ScheduledUpdateDayChoices =
        Enum.GetValues<DayOfWeek>()
            .Select(day => new SettingChoice(day.ToString(), day.ToString()))
            .ToList();

    public MainWindowViewModel(
        PlatformService platformService,
        CatalogService catalogService,
        SelectionService selectionService,
        DetectionService detectionService,
        InstallerService installerService,
        LoggingService loggingService,
        BrowserService browserService,
        SettingsService settingsService,
        ProfileService profileService,
        AppPreferencesService appPreferencesService,
        HistoryService historyService)
    {
        _platformService = platformService;
        _catalogService = catalogService;
        _selectionService = selectionService;
        _detectionService = detectionService;
        _installerService = installerService;
        _loggingService = loggingService;
        _browserService = browserService;
        _settingsService = settingsService;
        _profileService = profileService;
        _appPreferencesService = appPreferencesService;
        _dependencyResolverService = new DependencyResolverService(loggingService);
        _appUpdateService = new AppUpdateService(loggingService);
        _installerService.AppInstallStarted += HandleInstallerAppStarted;
        _installerService.AppInstallCompleted += HandleInstallerAppCompleted;

        Settings = new AppSettings();
        HistoryViewModel = new HistoryViewModel(historyService, loggingService);
        UpdatesViewModel = new UpdatesViewModel(UpdateAllAppsAsync, CheckForAppUpdatesAsync, UpdateSingleAppAsync);
        Settings.PropertyChanged += HandleSettingsChanged;
        LoggingService.DeveloperModeAccessor = () => Settings.DeveloperMode;
        _selectionService.SettingsAccessor = () => Settings;

        _installCommand = new AsyncRelayCommand(InstallSelectedAsync, CanInstall);
        _toggleViewModeCommand = new RelayCommand(_ => ToggleViewMode());
        _saveListCommand = new RelayCommand(_ => SaveCurrentList());
        _saveProfileCommand = new RelayCommand(_ => BeginSaveProfile(), _ => !IsInstalling && _isInitialized);
        _confirmSaveProfileCommand = new AsyncRelayCommand(ConfirmSaveProfileAsync, () => !IsInstalling && IsSaveProfilePanelOpen);
        _cancelSaveProfileCommand = new RelayCommand(_ => CancelSaveProfile(), _ => IsSaveProfilePanelOpen);
        _loadProfileCommand = new AsyncRelayCommand(LoadProfileAsync, () => !IsInstalling && _isInitialized);
        _exportProfileCommand = new AsyncRelayCommand(ExportProfileAsync, () => !IsInstalling && _isInitialized);
        _loadSavedProfileCommand = new AsyncRelayCommand(LoadSelectedSavedProfileAsync, () => !IsInstalling && SelectedSavedProfile is not null);
        _deleteSavedProfileCommand = new AsyncRelayCommand(DeleteSelectedSavedProfileAsync, () => !IsInstalling && SelectedSavedProfile is not null);
        _openLogFileCommand = new RelayCommand(_ => OpenLogFile());
        _showHelpCommand = new RelayCommand(_ => ShowHelpPlaceholder());
        _refreshCatalogCommand = new AsyncRelayCommand(RefreshCatalogAsync, () => !IsInstalling && _isInitialized);
        _showUpdatesFilterCommand = new RelayCommand(_ => ShowUpdatesFilter(), _ => !IsInstalling && HasUpdatesAvailable);
        _showRecommendedFilterCommand = new RelayCommand(_ => ShowRecommendedFilter(), _ => !IsInstalling && HasRecommendedApps);
        _dismissUpdateCommand = new RelayCommand(_ => DismissUpdateBanner(), _ => IsUpdateAvailable);
        _downloadUpdateCommand = new RelayCommand(_ => DownloadUpdate(), _ => IsUpdateAvailable && !string.IsNullOrWhiteSpace(UpdateDownloadUrl));
        _manualCheckForUpdatesCommand = new AsyncRelayCommand(() => CheckForUpdatesAsync(forceManualCheck: true), () => !IsInstalling);
        _runScheduledUpdatesNowCommand = new AsyncRelayCommand(RunScheduledUpdatesNowAsync, CanRunScheduledUpdatesNow);
        _clearLogsCommand = new AsyncRelayCommand(ClearLogsAsync, () => IsDeveloperModeEnabled);
        _copyLogsCommand = new AsyncRelayCommand(CopyLogsAsync, () => IsDeveloperModeEnabled && FilteredLiveLogs.Count > 0);
        _copySelectedLogCommand = new AsyncRelayCommand(CopySelectedLogAsync, () => IsDeveloperModeEnabled && SelectedLiveLogEntry is not null);
        _exportLogsCommand = new AsyncRelayCommand(ExportLogsAsync, () => IsDeveloperModeEnabled && FilteredLiveLogs.Count > 0);
        _browsePortableFolderCommand = new AsyncRelayCommand(BrowsePortableFolderAsync, () => !IsInstalling);
        _installAppCommand = new RelayCommand(InstallAppFromContextMenu, CanExecuteAppContextInstall);
        _updateAppCommand = new RelayCommand(UpdateAppFromContextMenu, CanExecuteAppContextUpdate);
        _uninstallCommand = new RelayCommand(UninstallAppFromContextMenu, CanExecuteAppContextUninstall);
        _openInstallLocationCommand = new RelayCommand(OpenInstallLocationFromContextMenu, CanExecuteAppContextInstalledAction);
        _copyToClipboardCommand = new RelayCommand(CopyToClipboardFromContextMenu);
        _toggleSilentInstallPreferenceCommand = new RelayCommand(ToggleSilentInstallPreference, CanExecuteAppContextParameter);
        _toggleScanningPreferenceCommand = new RelayCommand(ToggleScanningPreference, CanExecuteAppContextParameter);
        _toggleInstallScriptsPreferenceCommand = new RelayCommand(ToggleInstallScriptsPreference, CanExecuteAppContextParameter);
        _openPublisherCommand = new RelayCommand(OpenPublisherHomepage);
        _openAboutGitHubCommand = new RelayCommand(_ => OpenAboutGitHub(), _ => !IsInstalling);
        _showAppDetailsCommand = new RelayCommand(ShowAppDetails);
        _closeAppDetailsCommand = new RelayCommand(_ => CloseAppDetails());
        _pauseInstallCommand = new RelayCommand(_ => PauseInstallPlaceholder(), _ => IsInstalling);
        _clearQueueCommand = new RelayCommand(_ => ClearQueue(), _ => IsQueueVisible && !IsInstalling);
        _exportReportCommand = new RelayCommand(_ => ExportReportPlaceholder());
        _toggleSettingsPanelCommand = new RelayCommand(_ => ToggleSettingsPanel());
        _toggleAccountMenuCommand = new RelayCommand(_ => ToggleAccountMenu());
        _openAccountProfileCommand = new RelayCommand(_ => OpenAccountProfile(), _ => !IsInstalling);
        _openAccountSettingsCommand = new RelayCommand(_ => OpenAccountSettings());
        _navigateDashboardCommand = new RelayCommand(_ => NavigateTo(SectionDashboard), _ => !IsInstalling);
        _navigateAppsCommand = new RelayCommand(_ => NavigateTo(SectionApps), _ => !IsInstalling);
        _navigateDriversCommand = new RelayCommand(_ => NavigateToDriversFilter(), _ => !IsInstalling);
        _navigateMyListsCommand = new RelayCommand(_ => NavigateTo(SectionMyLists), _ => !IsInstalling);
        _navigateUpdatesCommand = new RelayCommand(_ => NavigateTo(SectionUpdates), _ => !IsInstalling);
        _navigateHistoryCommand = new RelayCommand(_ => NavigateTo(SectionHistory), _ => !IsInstalling);
        _navigateLogsCommand = new RelayCommand(_ => NavigateTo(SectionLogs), _ => !IsInstalling);
        _navigateAboutCommand = new RelayCommand(_ => NavigateTo(SectionAbout), _ => !IsInstalling);
        _openLogsFromSettingsCommand = new RelayCommand(_ => { IsSettingsPanelOpen = false; NavigateTo(SectionLogs); }, _ => !IsInstalling);
        _requestRestartNowCommand = new RelayCommand(_ => RequestRestartNow(), _ => CanShowRestartActions());
        _confirmRestartNowCommand = new AsyncRelayCommand(ConfirmRestartNowAsync, CanConfirmRestartNow);
        _cancelRestartCommand = new RelayCommand(_ => CancelRestartNow(), _ => IsRestartConfirmationVisible);
        _restartLaterCommand = new RelayCommand(_ => RestartLater(), _ => CanShowRestartActions());
        _resetSettingsCommand = new AsyncRelayCommand(ResetSettingsAsync, () => !IsInstalling);

        HookCollectionNotifications();
        RefreshFilteredLiveLogs();
    }

    public AppSettings Settings { get; }

    public HistoryViewModel HistoryViewModel { get; }

    public UpdatesViewModel UpdatesViewModel { get; }

    public string CurrentPlatform
    {
        get => _currentPlatform;
        private set => SetProperty(ref _currentPlatform, value);
    }

    public ObservableCollection<AppItem> Apps => _apps;

    public ObservableCollection<AppItem> VisibleApps => _visibleApps;

    public ObservableCollection<InstallQueueItem> InstallQueue => _installQueue;

    public ObservableCollection<NovaProfile> SavedProfiles => _savedProfiles;

    public ObservableCollection<LogEntry> LiveLogs => LoggingService.LiveLogs;

    public ObservableCollection<LogEntry> FilteredLiveLogs => _filteredLiveLogs;

    public int SelectedCount => _apps.Count(app => app.IsSelected);

    public int VisibleAppCount => _visibleApps.Count;

    public int RecommendedCount => _apps.Count(app => app.IsRecommended);

    public int UpdateAvailableCount => _apps.Count(app => app.HasUpdateAvailable);

    public int UnsupportedSelectedCount => _apps.Count(app => app.WillBeSkipped);

    public bool HasSavedProfiles => _savedProfiles.Count > 0;

    public bool HasNoSavedProfiles => !HasSavedProfiles;

    public bool HasRecommendedApps => RecommendedCount > 0;

    public bool HasUpdatesAvailable => UpdateAvailableCount > 0;

    public bool HasUnsupportedSelectedApps => UnsupportedSelectedCount > 0;

    public string SelectedFooterText => SelectedCount > 0 
        ? $"{SelectedCount} apps selected  •  {SelectedCount * 30} MB"
        : "0 apps selected";

    public int SelectedAppsCount => SelectedCount;
    public int SelectedAppsSizeMB => SelectedCount * 30;
    public int SelectedAppsTimeMins => SelectedCount * 2;

    public string RecommendedAppsSummary => HasRecommendedApps
        ? string.Join(" • ", _apps.Where(app => app.IsRecommended).Take(4).Select(app => app.Name))
        : "No hardware-based recommendations detected.";

    public string CurrentProfileName
    {
        get => _currentProfileName;
        private set => SetProperty(ref _currentProfileName, string.IsNullOrWhiteSpace(value) ? "Website Starter" : value.Trim());
    }

    public NovaProfile? SelectedSavedProfile
    {
        get => _selectedSavedProfile;
        set
        {
            if (SetProperty(ref _selectedSavedProfile, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public bool IsSaveProfilePanelOpen
    {
        get => _isSaveProfilePanelOpen;
        private set
        {
            if (SetProperty(ref _isSaveProfilePanelOpen, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public string PendingProfileName
    {
        get => _pendingProfileName;
        set => SetProperty(ref _pendingProfileName, string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim());
    }

    public string PendingProfileDescription
    {
        get => _pendingProfileDescription;
        set => SetProperty(ref _pendingProfileDescription, value ?? string.Empty);
    }

    public string HomeTitle => _currentSection switch
    {
        SectionDashboard => "Dashboard",
        SectionDrivers => "Drivers and accessories",
        SectionMyLists => "My list",
        SectionUpdates => "Available Updates",
        SectionHistory => "Downloads",
        _ => "Choose what to install"
    };

    public string HomeSubtitle => _currentSection switch
    {
        SectionDashboard => "Review apps, drivers, and setup choices from one place.",
        SectionDrivers => "Focused view for drivers, GPU tools, and accessory software.",
        SectionMyLists => "Only selected apps are shown here so you can review your list quickly.",
        SectionUpdates => "Review installed software that has a newer version available in the catalog.",
        SectionHistory => "Review the latest install activity and the full install history on this machine.",
        _ => "Pick apps, drivers, and packs, then press Install"
    };

    public string LogFilePath => _loggingService.LogFilePath;

    public string AboutVersionText => VersionService.GetFullVersionString();

    public string AboutCurrentVersionText => $"Current version: {VersionService.GetFullVersionString()}";

    public string AboutRemoteVersionText
    {
        get => _aboutRemoteVersionText;
        private set => SetProperty(ref _aboutRemoteVersionText, value ?? "Latest available: Not checked yet");
    }

    public string AboutGitHubUrl => NovaGitHubUrl;

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        private set
        {
            if (SetProperty(ref _isUpdateAvailable, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public string UpdateVersionText
    {
        get => _updateVersionText;
        private set => SetProperty(ref _updateVersionText, value ?? string.Empty);
    }

    public string UpdateDownloadUrl
    {
        get => _updateDownloadUrl;
        private set
        {
            if (SetProperty(ref _updateDownloadUrl, value ?? string.Empty))
            {
                UpdateCommandStates();
            }
        }
    }

    public bool IsDependencyInfoVisible
    {
        get => _isDependencyInfoVisible;
        private set => SetProperty(ref _isDependencyInfoVisible, value);
    }

    public string DependencyInfoText
    {
        get => _dependencyInfoText;
        private set => SetProperty(ref _dependencyInfoText, value ?? string.Empty);
    }

    public string AboutUpdateStatusText
    {
        get => _aboutUpdateStatusText;
        private set
        {
            if (SetProperty(ref _aboutUpdateStatusText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(IsAboutUpdateStatusVisible));
            }
        }
    }

    public bool IsAboutUpdateStatusVisible => !string.IsNullOrWhiteSpace(AboutUpdateStatusText);

    public SettingChoice? SelectedLogLevelFilterOption
    {
        get => LogLevelFilterChoices.FirstOrDefault(choice => choice.Value == _selectedLogLevelFilter) ?? LogLevelFilterChoices[0];
        set
        {
            if (value is null || value.Value == _selectedLogLevelFilter)
            {
                return;
            }

            _selectedLogLevelFilter = value.Value;
            OnPropertyChanged();
            RefreshFilteredLiveLogs();
        }
    }

    public LogEntry? SelectedLiveLogEntry
    {
        get => _selectedLiveLogEntry;
        set
        {
            if (SetProperty(ref _selectedLiveLogEntry, value))
            {
                _copySelectedLogCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsDashboardSelected => string.Equals(_currentSection, SectionDashboard, StringComparison.Ordinal);
    public bool IsDashboardUnselected => !IsDashboardSelected;

    public bool IsAppsSelected => string.Equals(_currentSection, SectionApps, StringComparison.Ordinal);
    public bool IsAppsUnselected => !IsAppsSelected;

    public bool IsDriversSelected => string.Equals(_currentSection, SectionDrivers, StringComparison.Ordinal);
    public bool IsDriversUnselected => !IsDriversSelected;

    public bool IsMyListsSelected => string.Equals(_currentSection, SectionMyLists, StringComparison.Ordinal);
    public bool IsMyListsUnselected => !IsMyListsSelected;

    public bool IsUpdatesSelected => string.Equals(_currentSection, SectionUpdates, StringComparison.Ordinal);
    public bool IsUpdatesUnselected => !IsUpdatesSelected;

    public bool IsHistorySelected => string.Equals(_currentSection, SectionHistory, StringComparison.Ordinal);
    public bool IsHistoryUnselected => !IsHistorySelected;

    public bool IsLogsSelected => string.Equals(_currentSection, SectionLogs, StringComparison.Ordinal);
    public bool IsLogsUnselected => !IsLogsSelected;

    public bool IsAboutSelected => string.Equals(_currentSection, SectionAbout, StringComparison.Ordinal);
    public bool IsAboutUnselected => !IsAboutSelected;

    public bool IsHomeScreenVisible => !IsInstalling && !IsQueueVisible && !IsUpdatesSelected && !IsHistorySelected && !IsLogsSelected && !IsAboutSelected;

    public bool IsListViewActive => !_isGridViewActive;

    public bool IsGridViewActive
    {
        get => _isGridViewActive;
        set
        {
            if (_isGridViewActive != value)
            {
                _isGridViewActive = value;
                OnPropertyChanged(nameof(IsGridViewActive));
                OnPropertyChanged(nameof(IsListViewActive));
            }
        }
    }

    public System.Windows.Input.ICommand ToggleViewModeCommand => _toggleViewModeCommand;

    private void ToggleViewMode()
    {
        IsGridViewActive = !IsGridViewActive;
    }

    public bool IsInstallScreenVisible => IsInstalling || IsQueueVisible;

    public bool IsSummaryScreenVisible => !IsInstalling && !IsQueueVisible && IsHistorySelected && HasInstallResults;

    public bool IsHistoryEmptyScreenVisible => !IsInstalling && !IsQueueVisible && IsHistorySelected && !HasInstallResults;

    public bool IsHistoryScreenVisible => !IsInstalling && !IsQueueVisible && IsHistorySelected;

    public bool IsLogsScreenVisible => !IsInstalling && !IsQueueVisible && IsLogsSelected;

    public bool IsAboutScreenVisible => !IsInstalling && !IsQueueVisible && IsAboutSelected;

    public bool IsUpdatesScreenVisible => !IsInstalling && !IsQueueVisible && IsUpdatesSelected;

    public bool IsSelectionPhase => !IsQueueVisible && !IsUpdatesSelected && !IsHistorySelected && !IsLogsSelected && !IsAboutSelected;

    public bool IsSettingsPanelOpen
    {
        get => _isSettingsPanelOpen;
        private set => SetProperty(ref _isSettingsPanelOpen, value);
    }

    public bool IsAccountMenuOpen
    {
        get => _isAccountMenuOpen;
        private set
        {
            if (SetProperty(ref _isAccountMenuOpen, value))
            {
                OnPropertyChanged(nameof(IsAccountMenuClosed));
            }
        }
    }

    public bool IsAccountMenuClosed => !IsAccountMenuOpen;

    public bool IsDeveloperModeEnabled => Settings.DeveloperMode;

    public bool IsCustomDownloadFolderVisible => string.Equals(
        Settings.DownloadLocationMode,
        AppSettings.DownloadCustomFolder,
        StringComparison.Ordinal);

    public IReadOnlyList<SettingChoice> RestartBehaviorOptions => RestartBehaviorChoices;

    public IReadOnlyList<SettingChoice> DownloadLocationOptions => DownloadLocationChoices;

    public IReadOnlyList<SettingChoice> ThemeOptions => ThemeChoices;

    public IReadOnlyList<SettingChoice> LanguageOptions => LanguageChoices;

    public IReadOnlyList<SettingChoice> LogLevelFilterOptions => LogLevelFilterChoices;

    public IReadOnlyList<SettingChoice> ScheduledUpdateFrequencyOptions => ScheduledUpdateFrequencyChoices;

    public IReadOnlyList<SettingChoice> ScheduledUpdateHourOptions => ScheduledUpdateHourChoices;

    public IReadOnlyList<SettingChoice> ScheduledUpdateDayOptions => ScheduledUpdateDayChoices;

    public SettingChoice? SelectedRestartBehaviorOption
    {
        get => RestartBehaviorChoices.FirstOrDefault(choice => choice.Value == Settings.RestartBehavior) ?? RestartBehaviorChoices[0];
        set
        {
            if (value is null || value.Value == Settings.RestartBehavior)
            {
                return;
            }

            Settings.RestartBehavior = value.Value;
            OnPropertyChanged();
        }
    }

    public SettingChoice? SelectedDownloadLocationOption
    {
        get => DownloadLocationChoices.FirstOrDefault(choice => choice.Value == Settings.DownloadLocationMode) ?? DownloadLocationChoices[0];
        set
        {
            if (value is null || value.Value == Settings.DownloadLocationMode)
            {
                return;
            }

            Settings.DownloadLocationMode = value.Value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCustomDownloadFolderVisible));
        }
    }

    public SettingChoice? SelectedThemeOption
    {
        get => ThemeChoices.FirstOrDefault(choice => choice.Value == Settings.Theme) ?? ThemeChoices[0];
        set
        {
            if (value is null || value.Value == Settings.Theme)
            {
                return;
            }

            Settings.Theme = value.Value;
            OnPropertyChanged();
        }
    }

    public SettingChoice? SelectedLanguageOption
    {
        get => LanguageChoices.FirstOrDefault(choice => choice.Value == Settings.Language) ?? LanguageChoices[0];
        set
        {
            if (value is null || value.Value == Settings.Language)
            {
                return;
            }

            Settings.Language = value.Value;
            OnPropertyChanged();
        }
    }

    public SettingChoice? SelectedScheduledUpdateFrequencyOption
    {
        get => ScheduledUpdateFrequencyChoices.FirstOrDefault(
                   choice => string.Equals(choice.Value, Settings.ScheduledUpdateFrequency, StringComparison.OrdinalIgnoreCase))
               ?? ScheduledUpdateFrequencyChoices[1];
        set
        {
            if (value is null ||
                string.Equals(value.Value, Settings.ScheduledUpdateFrequency, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Settings.ScheduledUpdateFrequency = value.Value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsScheduledUpdateWeeklyVisible));
            OnPropertyChanged(nameof(ScheduledUpdatesNextRunText));
        }
    }

    public SettingChoice? SelectedScheduledUpdateHourOption
    {
        get => ScheduledUpdateHourChoices.FirstOrDefault(choice => choice.Value == Settings.ScheduledUpdateHour.ToString())
               ?? ScheduledUpdateHourChoices[Math.Clamp(Settings.ScheduledUpdateHour, 0, 23)];
        set
        {
            if (value is null || !int.TryParse(value.Value, out var hour) || hour == Settings.ScheduledUpdateHour)
            {
                return;
            }

            Settings.ScheduledUpdateHour = hour;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ScheduledUpdatesNextRunText));
        }
    }

    public SettingChoice? SelectedScheduledUpdateDayOption
    {
        get => ScheduledUpdateDayChoices.FirstOrDefault(
                   choice => string.Equals(choice.Value, Settings.ScheduledUpdateDay.ToString(), StringComparison.Ordinal))
               ?? ScheduledUpdateDayChoices[(int)Settings.ScheduledUpdateDay];
        set
        {
            if (value is null ||
                !Enum.TryParse<DayOfWeek>(value.Value, ignoreCase: true, out var day) ||
                day == Settings.ScheduledUpdateDay)
            {
                return;
            }

            Settings.ScheduledUpdateDay = day;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ScheduledUpdatesNextRunText));
        }
    }

    public bool AreScheduledUpdateControlsEnabled => Settings.ScheduledUpdatesEnabled;

    public bool IsScheduledUpdateWeeklyVisible => string.Equals(
        Settings.ScheduledUpdateFrequency,
        AppSettings.ScheduledFrequencyWeekly,
        StringComparison.OrdinalIgnoreCase);

    public string ScheduledUpdatesLastRunText => Settings.LastScheduledUpdateRun == DateTime.MinValue
        ? "Never"
        : FormatScheduledTimestamp(Settings.LastScheduledUpdateRun);

    public string ScheduledUpdatesNextRunText
    {
        get
        {
            if (!Settings.ScheduledUpdatesEnabled)
            {
                return "Disabled";
            }

            var nextRun = _scheduledUpdateService?.NextScheduledRun ??
                          ScheduledUpdateService.CalculateNextScheduledRun(Settings);
            if (nextRun == DateTime.MinValue)
            {
                return "Not scheduled";
            }

            return FormatScheduledTimestamp(nextRun);
        }
    }

    public string ScheduledUpdateTaskSchedulerStatusText =>
        _scheduledUpdateService?.IsTaskRegistered == true ? "Registered" : "Not registered";

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyVisibilityFilter();
            }
        }
    }

    public string SelectedFilter
    {
        get => _selectedFilter;
        private set => SetProperty(ref _selectedFilter, value);
    }

    public bool IsAllFilter
    {
        get => _isAllFilter;
        set
        {
            if (SetProperty(ref _isAllFilter, value) && value && !_updatingFilterFlags)
            {
                SetCategoryFilter("All");
            }
        }
    }

    public bool IsGamesFilter
    {
        get => _isGamesFilter;
        set
        {
            if (SetProperty(ref _isGamesFilter, value) && value && !_updatingFilterFlags)
            {
                SetCategoryFilter("Games");
            }
        }
    }

    public bool IsDevToolsFilter
    {
        get => _isDevToolsFilter;
        set
        {
            if (SetProperty(ref _isDevToolsFilter, value) && value && !_updatingFilterFlags)
            {
                SetCategoryFilter("Dev Tools");
            }
        }
    }

    public bool IsDriversFilter
    {
        get => _isDriversFilter;
        set
        {
            if (SetProperty(ref _isDriversFilter, value) && value && !_updatingFilterFlags)
            {
                SetCategoryFilter("Drivers");
            }
        }
    }

    public bool IsRecommendedFilter
    {
        get => _isRecommendedFilter;
        set
        {
            if (SetProperty(ref _isRecommendedFilter, value) && value && !_updatingFilterFlags)
            {
                SetCategoryFilter("Recommended");
            }
        }
    }

    public bool IsUtilitiesFilter
    {
        get => _isUtilitiesFilter;
        set
        {
            if (SetProperty(ref _isUtilitiesFilter, value) && value && !_updatingFilterFlags)
            {
                SetCategoryFilter("Utilities");
            }
        }
    }

    public bool IsArm64Filter
    {
        get => _isArm64Filter;
        set
        {
            if (SetProperty(ref _isArm64Filter, value) && value && !_updatingFilterFlags)
            {
                SetCategoryFilter("ARM64");
            }
        }
    }

    public bool IsUpdatesFilter
    {
        get => _isUpdatesFilter;
        set
        {
            if (SetProperty(ref _isUpdatesFilter, value) && value && !_updatingFilterFlags)
            {
                SetCategoryFilter("Updates");
            }
        }
    }

    public bool IsInstalling
    {
        get => _isInstalling;
        private set
        {
            if (SetProperty(ref _isInstalling, value))
            {
                NotifyScreenStateChanged();
                UpdateCommandStates();
            }
        }
    }

    public bool IsQueueVisible
    {
        get => _isQueueVisible;
        private set
        {
            if (SetProperty(ref _isQueueVisible, value))
            {
                OnPropertyChanged(nameof(InstallQueueHeaderText));
                NotifyScreenStateChanged();
                UpdateCommandStates();
            }
        }
    }

    public string InstallStatusText
    {
        get => _installStatusText;
        private set => SetProperty(ref _installStatusText, value);
    }

    public string InstallQueueHeaderText
    {
        get
        {
            if (_installQueue.Count == 0)
            {
                return "Install queue";
            }

            if (IsInstalling)
            {
                var activeIndex = _installQueue
                    .Select((item, index) => new { item, index })
                    .FirstOrDefault(entry => entry.item.IsActive)?.index ?? -1;
                var currentPosition = activeIndex >= 0
                    ? activeIndex + 1
                    : Math.Min(_installQueue.Count, _installQueue.Count(item => item.Status != InstallQueueStatus.Pending) + 1);
                return $"Installing {currentPosition} of {_installQueue.Count}...";
            }

            var doneCount = _installQueue.Count(item => item.Status == InstallQueueStatus.Done);
            var failedCount = _installQueue.Count(item => item.Status == InstallQueueStatus.Failed);
            var skippedCount = _installQueue.Count(item => item.Status == InstallQueueStatus.Skipped);
            var cancelledCount = _installQueue.Count(item => item.Status == InstallQueueStatus.Cancelled);

            var summaryParts = new List<string> { $"{doneCount} done" };
            if (failedCount > 0)
            {
                summaryParts.Add($"{failedCount} failed");
            }

            if (skippedCount > 0)
            {
                summaryParts.Add($"{skippedCount} skipped");
            }

            if (cancelledCount > 0)
            {
                summaryParts.Add($"{cancelledCount} cancelled");
            }

            return $"Finished - {string.Join(", ", summaryParts)}";
        }
    }

    public string InstallSummaryText
    {
        get => _installSummaryText;
        private set => SetProperty(ref _installSummaryText, value);
    }

    public ObservableCollection<InstallResult> InstallResults => _installResults;

    public ObservableCollection<InstallResult> InstalledResults => _installedResults;

    public ObservableCollection<InstallResult> FailedResults => _failedResults;

    public ObservableCollection<InstallResult> SkippedResults => _skippedResults;

    public ObservableCollection<InstallResult> RestartRequiredResults => _restartRequiredResults;

    public ObservableCollection<InstallResult> UnsupportedSkippedResults => _unsupportedSkippedResults;

    public bool HasInstallResults => _installResults.Count > 0;

    public string DashboardHistoryText => HasInstallResults
        ? InstallSummaryText
        : "No install history yet. Run an install and the summary screen will appear here.";

    public bool HasInstalledResults => _installedResults.Count > 0;

    public bool HasFailedResults => _failedResults.Count > 0;

    public bool HasSkippedResults => _skippedResults.Count > 0;

    public bool HasRestartRequiredResults => _restartRequiredResults.Count > 0;

    public bool HasUnsupportedSkippedResults => _unsupportedSkippedResults.Count > 0;

    public bool RestartRequired
    {
        get => _restartRequired;
        private set
        {
            if (SetProperty(ref _restartRequired, value))
            {
                OnPropertyChanged(nameof(IsRestartSectionVisible));
                OnPropertyChanged(nameof(ShowRestartActions));
                OnPropertyChanged(nameof(ShowPrimaryRestartActions));
                UpdateCommandStates();
            }
        }
    }

    public bool IsRestartSectionVisible => RestartRequired && HasInstallResults;

    public bool ShowRestartActions => RestartRequired && !_restartDecisionFinalized;

    public bool ShowPrimaryRestartActions => ShowRestartActions && !IsRestartConfirmationVisible;

    public bool IsRestartConfirmationVisible
    {
        get => _isRestartConfirmationVisible;
        private set
        {
            if (SetProperty(ref _isRestartConfirmationVisible, value))
            {
                OnPropertyChanged(nameof(ShowPrimaryRestartActions));
                UpdateCommandStates();
            }
        }
    }

    public string RestartStatusText
    {
        get => _restartStatusText;
        private set => SetProperty(ref _restartStatusText, value);
    }

    public bool IsAppDetailsOpen
    {
        get => _isAppDetailsOpen;
        private set => SetProperty(ref _isAppDetailsOpen, value);
    }

    public AppItem? SelectedDetailApp
    {
        get => _selectedDetailApp;
        private set
        {
            if (SetProperty(ref _selectedDetailApp, value))
            {
                NotifyAppDetailsStateChanged();
            }
        }
    }

    public string SelectedDetailDescription =>
        string.IsNullOrWhiteSpace(SelectedDetailApp?.Description)
            ? "No description is available for this app yet."
            : SelectedDetailApp.Description;

    public string SelectedDetailPlatformsText
    {
        get
        {
            if (SelectedDetailApp?.SupportedPlatforms is null)
            {
                return "No platform metadata";
            }

            var supportedPlatforms = new List<string>();
            if (SelectedDetailApp.SupportedPlatforms.Windows)
            {
                supportedPlatforms.Add("Windows");
            }

            if (SelectedDetailApp.SupportedPlatforms.Linux)
            {
                supportedPlatforms.Add("Linux");
            }

            return supportedPlatforms.Count == 0 ? "No supported platforms listed" : string.Join(" • ", supportedPlatforms);
        }
    }

    public string SelectedDetailInstallSupportText =>
        SelectedDetailApp is null
            ? string.Empty
            : SelectedDetailApp.SupportsSilentInstall
                ? "Silent install is available when the package supports it."
                : "This app may require an interactive or manual installer flow.";

    public string SelectedDetailSupportStatusText =>
        SelectedDetailApp is null
            ? string.Empty
            : SelectedDetailApp.IsSupportedOnCurrentPlatform
                ? $"Supported on {CurrentPlatform}"
                : $"Unsupported on {CurrentPlatform}; it will be skipped if selected.";

    public string SelectedDetailCatalogVersionText =>
        SelectedDetailApp is null
            ? string.Empty
            : string.IsNullOrWhiteSpace(SelectedDetailApp.Version)
                ? "Catalog version: not listed"
                : $"Catalog version: {SelectedDetailApp.Version}";

    public string SelectedDetailInstalledVersionText =>
        SelectedDetailApp is null
            ? string.Empty
            : string.IsNullOrWhiteSpace(SelectedDetailApp.InstalledVersion)
                ? "Installed version: not detected"
                : $"Installed version: {SelectedDetailApp.InstalledVersion}";

    public string SelectedDetailUpdateStatusText =>
        SelectedDetailApp is null
            ? string.Empty
            : SelectedDetailApp.HasUpdateAvailable
                ? $"Update available: {SelectedDetailApp.InstalledVersion} -> {SelectedDetailApp.Version}"
                : SelectedDetailApp.IsInstalled
                    ? "Update status: up to date"
                    : "Update status: not installed";

    public bool HasSelectedDetailRecommendation =>
        SelectedDetailApp?.IsRecommended == true &&
        !string.IsNullOrWhiteSpace(SelectedDetailApp.RecommendationReason);

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public ICommand InstallCommand => _installCommand;

    public ICommand SaveListCommand => _saveListCommand;

    public ICommand SaveProfileCommand => _saveProfileCommand;

    public ICommand ConfirmSaveProfileCommand => _confirmSaveProfileCommand;

    public ICommand CancelSaveProfileCommand => _cancelSaveProfileCommand;

    public ICommand LoadProfileCommand => _loadProfileCommand;

    public ICommand ExportProfileCommand => _exportProfileCommand;

    public ICommand LoadSavedProfileCommand => _loadSavedProfileCommand;

    public ICommand DeleteSavedProfileCommand => _deleteSavedProfileCommand;

    public ICommand OpenLogFileCommand => _openLogFileCommand;

    public ICommand ShowHelpCommand => _showHelpCommand;

    public ICommand HelpCommand => _showHelpCommand;

    public ICommand RefreshCatalogCommand => _refreshCatalogCommand;

    public ICommand ShowUpdatesFilterCommand => _showUpdatesFilterCommand;

    public ICommand ShowRecommendedFilterCommand => _showRecommendedFilterCommand;

    public ICommand DismissUpdateCommand => _dismissUpdateCommand;

    public ICommand DownloadUpdateCommand => _downloadUpdateCommand;

    public ICommand ManualCheckForUpdatesCommand => _manualCheckForUpdatesCommand;

    public ICommand RunScheduledUpdatesNowCommand => _runScheduledUpdatesNowCommand;

    public ICommand ClearLogsCommand => _clearLogsCommand;

    public ICommand CopyLogsCommand => _copyLogsCommand;

    public ICommand CopySelectedLogCommand => _copySelectedLogCommand;

    public ICommand ExportLogsCommand => _exportLogsCommand;

    public ICommand BrowsePortableFolderCommand => _browsePortableFolderCommand;

    public ICommand InstallAppCommand => _installAppCommand;

    public ICommand UpdateAppCommand => _updateAppCommand;

    public ICommand UninstallCommand => _uninstallCommand;

    public ICommand OpenInstallLocationCommand => _openInstallLocationCommand;

    public ICommand CopyToClipboardCommand => _copyToClipboardCommand;

    public ICommand ToggleSilentInstallPreferenceCommand => _toggleSilentInstallPreferenceCommand;

    public ICommand ToggleScanningPreferenceCommand => _toggleScanningPreferenceCommand;

    public ICommand ToggleInstallScriptsPreferenceCommand => _toggleInstallScriptsPreferenceCommand;

    public ICommand OpenPublisherCommand => _openPublisherCommand;

    public ICommand OpenAboutGitHubCommand => _openAboutGitHubCommand;

    public ICommand ShowAppDetailsCommand => _showAppDetailsCommand;

    public ICommand CloseAppDetailsCommand => _closeAppDetailsCommand;

    public ICommand PauseInstallCommand => _pauseInstallCommand;

    public ICommand ClearQueueCommand => _clearQueueCommand;

    public ICommand ExportReportCommand => _exportReportCommand;

    public ICommand ToggleSettingsPanelCommand => _toggleSettingsPanelCommand;

    public ICommand ToggleAccountMenuCommand => _toggleAccountMenuCommand;

    public ICommand OpenAccountProfileCommand => _openAccountProfileCommand;

    public ICommand OpenAccountSettingsCommand => _openAccountSettingsCommand;

    public ICommand NavigateDashboardCommand => _navigateDashboardCommand;

    public ICommand NavigateAppsCommand => _navigateAppsCommand;

    public ICommand NavigateDriversCommand => _navigateDriversCommand;

    public ICommand NavigateMyListsCommand => _navigateMyListsCommand;

    public ICommand NavigateUpdatesCommand => _navigateUpdatesCommand;

    public ICommand NavigateHistoryCommand => _navigateHistoryCommand;

    public ICommand NavigateLogsCommand => _navigateLogsCommand;
    public ICommand OpenLogsFromSettingsCommand => _openLogsFromSettingsCommand;

    public ICommand NavigateAboutCommand => _navigateAboutCommand;

    public ICommand UpdateAllCommand => UpdatesViewModel.UpdateAllCommand;

    public ICommand CheckForAppUpdatesCommand => UpdatesViewModel.CheckNowCommand;

    public ICommand RequestRestartNowCommand => _requestRestartNowCommand;

    public ICommand ConfirmRestartNowCommand => _confirmRestartNowCommand;

    public ICommand CancelRestartCommand => _cancelRestartCommand;

    public ICommand RestartLaterCommand => _restartLaterCommand;

    public ICommand ResetSettingsCommand => _resetSettingsCommand;

    public Task InitializeAsync()
    {
        return InitializeCoreAsync(updateStatusAsync: null, runDetectionInBackground: true);
    }

    public Task InitializeWithSplashAsync(Func<string, Task> updateStatusAsync)
    {
        ArgumentNullException.ThrowIfNull(updateStatusAsync);
        return InitializeCoreAsync(updateStatusAsync, runDetectionInBackground: false);
    }

    public void AttachScheduledUpdateService(ScheduledUpdateService scheduledUpdateService)
    {
        _scheduledUpdateService = scheduledUpdateService ?? throw new ArgumentNullException(nameof(scheduledUpdateService));
        OnPropertyChanged(nameof(ScheduledUpdatesNextRunText));
        OnPropertyChanged(nameof(ScheduledUpdateTaskSchedulerStatusText));
        UpdateCommandStates();
    }

    public List<AppItem> CreateScheduledUpdateSnapshot()
    {
        return _apps.Select(CloneAppForBackgroundWork).ToList();
    }

    private async Task InitializeCoreAsync(
        Func<string, Task>? updateStatusAsync,
        bool runDetectionInBackground)
    {
        if (_isInitialized || _isInitializing)
        {
            return;
        }

        _isInitializing = true;

        try
        {
            await LoadSettingsAsync();

            var platform = _platformService.GetCurrentPlatformInfo();
            _currentPlatformId = platform.Id;
            CurrentPlatform = platform.Label;

            if (updateStatusAsync is not null)
            {
                await updateStatusAsync("Loading catalog...");
            }

            var apps = await Task.Run(() => _catalogService.LoadAppsAsync(_currentPlatformId));
            await _appPreferencesService.ApplyToAppsAsync(apps);
            var selection = _selectionService.LoadSelection();
            ApplySelectionProfile(selection);

            if (_settingsService.LoadedDefaultsOnLastLoad)
            {
                ApplySelectionSettingsFallback(selection);
            }

            _selectionService.ApplySelection(apps, selection);

            _apps.Clear();
            foreach (var app in apps)
            {
                ApplyPlatformFlags(app);
                app.PropertyChanged += HandleAppPropertyChanged;
                _apps.Add(app);
            }

            ApplyVisibilityFilter();
            NotifyAppSummaryStateChanged();
            NotifySettingsOptionBindingsChanged();
            NotifyScreenStateChanged();
            UpdateCommandStates();
            await RefreshSavedProfilesAsync();
            await HistoryViewModel.RefreshAsync();

            if (runDetectionInBackground)
            {
                StatusText = BuildLoadedStatusText(detectionPending: true);
                _loggingService.LogInfo(StatusText);
                StartBackgroundStartupDetection();
            }
            else
            {
                await RunStartupDetectionInlineAsync(updateStatusAsync);
            }

            _isInitialized = true;
            _ = CheckForUpdatesAsync(forceManualCheck: false);
        }
        catch
        {
            _isInitialized = false;
            throw;
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private void HookCollectionNotifications()
    {
        _installResults.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasInstallResults));
            OnPropertyChanged(nameof(IsRestartSectionVisible));
            OnPropertyChanged(nameof(DashboardHistoryText));
            NotifyScreenStateChanged();
        };

        _installedResults.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasInstalledResults));
        _failedResults.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasFailedResults));
        _skippedResults.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasSkippedResults));
        _restartRequiredResults.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasRestartRequiredResults));
        _unsupportedSkippedResults.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasUnsupportedSkippedResults));
        _visibleApps.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(VisibleAppCount));
            OnPropertyChanged(nameof(SelectedFooterText));
            OnPropertyChanged(nameof(SelectedAppsCount));
            OnPropertyChanged(nameof(SelectedAppsSizeMB));
            OnPropertyChanged(nameof(SelectedAppsTimeMins));
        };
        _installQueue.CollectionChanged += HandleInstallQueueCollectionChanged;
        _savedProfiles.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasSavedProfiles));
            OnPropertyChanged(nameof(HasNoSavedProfiles));
            _loadSavedProfileCommand.RaiseCanExecuteChanged();
            _deleteSavedProfileCommand.RaiseCanExecuteChanged();
        };
        LoggingService.LiveLogs.CollectionChanged += HandleLiveLogsCollectionChanged;
    }

    private void HandleInstallQueueCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var oldItem in e.OldItems.OfType<InstallQueueItem>())
            {
                oldItem.PropertyChanged -= HandleInstallQueueItemPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var newItem in e.NewItems.OfType<InstallQueueItem>())
            {
                newItem.PropertyChanged += HandleInstallQueueItemPropertyChanged;
            }
        }

        IsQueueVisible = _installQueue.Count > 0;
        OnPropertyChanged(nameof(InstallQueueHeaderText));
    }

    private void HandleInstallQueueItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstallQueueItem.Status) ||
            e.PropertyName == nameof(InstallQueueItem.IsActive) ||
            e.PropertyName == nameof(InstallQueueItem.StatusText))
        {
            OnPropertyChanged(nameof(InstallQueueHeaderText));
        }
    }

    private void HandleLiveLogsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Marshal to UI thread to safely mutate UI-bound collections
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            RefreshFilteredLiveLogs();
            OnPropertyChanged(nameof(LiveLogs));
        });
    }

    private void HandleInstallerAppStarted(AppItem app)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var trackedApp = _apps.FirstOrDefault(candidate =>
                candidate.Id.Equals(app.Id, StringComparison.OrdinalIgnoreCase));
            if (trackedApp is null)
            {
                return;
            }

            trackedApp.IsCancellable = true;
            trackedApp.CancelCommand = new RelayCommand(_ => CancelSingleInstall(trackedApp), _ => trackedApp.IsCancellable);
            trackedApp.StatusBadge = AppItem.StatusInstalling;

            if (ReferenceEquals(SelectedDetailApp, trackedApp))
            {
                NotifyAppDetailsStateChanged();
            }
        });
    }

    private void HandleInstallerAppCompleted(AppItem app)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var trackedApp = _apps.FirstOrDefault(candidate =>
                candidate.Id.Equals(app.Id, StringComparison.OrdinalIgnoreCase));
            if (trackedApp is null)
            {
                return;
            }

            trackedApp.IsCancellable = false;
            trackedApp.CancelCommand = null;

            if (ReferenceEquals(SelectedDetailApp, trackedApp))
            {
                NotifyAppDetailsStateChanged();
            }
        });
    }

    private void HandleSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName))
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(AppSettings.SilentInstall):
                RefreshAllAppVisualStates();
                break;
            case nameof(AppSettings.OsSupportedApps):
            case nameof(AppSettings.ShowAlreadyInstalledApps):
                RefreshAllAppVisualStates();
                break;
            case nameof(AppSettings.AutoDetectInstalledAppsOnStartup):
                if (Settings.AutoDetectInstalledAppsOnStartup && !_installedAppsDetected)
                {
                    DetectInstalledAppsNow();
                }
                break;
            case nameof(AppSettings.DownloadLocationMode):
                OnPropertyChanged(nameof(SelectedDownloadLocationOption));
                OnPropertyChanged(nameof(IsCustomDownloadFolderVisible));
                break;
            case nameof(AppSettings.RestartBehavior):
                OnPropertyChanged(nameof(SelectedRestartBehaviorOption));
                break;
            case nameof(AppSettings.Theme):
                OnPropertyChanged(nameof(SelectedThemeOption));
                ApplyTheme();
                break;
            case nameof(AppSettings.Language):
                OnPropertyChanged(nameof(SelectedLanguageOption));
                break;
            case nameof(AppSettings.ScheduledUpdatesEnabled):
                OnPropertyChanged(nameof(AreScheduledUpdateControlsEnabled));
                OnPropertyChanged(nameof(ScheduledUpdatesNextRunText));
                OnPropertyChanged(nameof(ScheduledUpdateTaskSchedulerStatusText));
                _runScheduledUpdatesNowCommand.RaiseCanExecuteChanged();
                break;
            case nameof(AppSettings.ScheduledUpdateFrequency):
                OnPropertyChanged(nameof(SelectedScheduledUpdateFrequencyOption));
                OnPropertyChanged(nameof(IsScheduledUpdateWeeklyVisible));
                OnPropertyChanged(nameof(ScheduledUpdatesNextRunText));
                OnPropertyChanged(nameof(ScheduledUpdateTaskSchedulerStatusText));
                break;
            case nameof(AppSettings.ScheduledUpdateHour):
                OnPropertyChanged(nameof(SelectedScheduledUpdateHourOption));
                OnPropertyChanged(nameof(ScheduledUpdatesNextRunText));
                OnPropertyChanged(nameof(ScheduledUpdateTaskSchedulerStatusText));
                break;
            case nameof(AppSettings.ScheduledUpdateDay):
                OnPropertyChanged(nameof(SelectedScheduledUpdateDayOption));
                OnPropertyChanged(nameof(ScheduledUpdatesNextRunText));
                OnPropertyChanged(nameof(ScheduledUpdateTaskSchedulerStatusText));
                break;
            case nameof(AppSettings.RunMissedUpdatesASAP):
                OnPropertyChanged(nameof(ScheduledUpdatesNextRunText));
                OnPropertyChanged(nameof(ScheduledUpdateTaskSchedulerStatusText));
                break;
            case nameof(AppSettings.LastScheduledUpdateRun):
                OnPropertyChanged(nameof(ScheduledUpdatesLastRunText));
                OnPropertyChanged(nameof(ScheduledUpdatesNextRunText));
                break;
            case nameof(AppSettings.DeveloperMode):
                OnPropertyChanged(nameof(IsDeveloperModeEnabled));
                NotifyScreenStateChanged();
                RefreshFilteredLiveLogs();
                _clearLogsCommand.RaiseCanExecuteChanged();
                _copyLogsCommand.RaiseCanExecuteChanged();
                _copySelectedLogCommand.RaiseCanExecuteChanged();
                _exportLogsCommand.RaiseCanExecuteChanged();
                break;
        }

        if (_suppressSettingsLogging)
        {
            return;
        }

        var skipDeferredPersistence = string.Equals(
            e.PropertyName,
            nameof(AppSettings.LastScheduledUpdateRun),
            StringComparison.Ordinal);

        if (!skipDeferredPersistence)
        {
            LogSettingChanged(e.PropertyName);
            ScheduleSettingsSave();
        }

        if (!skipDeferredPersistence && Settings.SaveProfilesAutomatically)
        {
            SaveCurrentList(showStatusMessage: false);
            _ = _selectionService.SaveAutoProfileAsync(GetSelectedAppIds());
            _ = RefreshSavedProfilesAsync();
        }
    }

    private async Task LoadSettingsAsync()
    {
        var loadedSettings = await _settingsService.LoadSettingsAsync();
        _suppressSettingsLogging = true;
        try
        {
            Settings.ApplyFrom(loadedSettings);
        }
        finally
        {
            _suppressSettingsLogging = false;
        }

        ApplyTheme();
        NotifySettingsOptionBindingsChanged();
    }

    private void RefreshAllAppVisualStates()
    {
        foreach (var app in _apps)
        {
            ApplyPlatformFlags(app);
        }

        ApplyVisibilityFilter();
        NotifyAppSummaryStateChanged();
        UpdateCommandStates();
    }

    private void ApplySelectionProfile(SelectionConfig? selection)
    {
        if (selection is null)
        {
            return;
        }

        CurrentProfileName = selection.ProfileName;
        _loggingService.LogInfo($"Loaded selection profile '{CurrentProfileName}'.");
    }

    private void ApplySelectionSettingsFallback(SelectionConfig? selection)
    {
        if (selection?.Settings is null)
        {
            return;
        }

        var selectionSettings = selection.Settings;

        _suppressSettingsLogging = true;
        try
        {
            Settings.SilentInstall = selectionSettings.SilentInstall;
            Settings.OsSupportedApps = selectionSettings.OsSupportedApps;
            Settings.SelfDeleteAfterInstall = selectionSettings.SelfDelete;

            if (!string.IsNullOrWhiteSpace(selectionSettings.DefaultInstallLocation))
            {
                Settings.DownloadLocationMode = AppSettings.DownloadCustomFolder;
                Settings.CustomDownloadFolder = selectionSettings.DefaultInstallLocation;
            }
        }
        finally
        {
            _suppressSettingsLogging = false;
        }

        _loggingService.LogInfo("Applied compatibility settings from selection.json because settings.json was using defaults.");
        _ = _settingsService.SaveSettingsAsync(Settings.Clone());
        NotifySettingsOptionBindingsChanged();
    }

    private void NotifySettingsOptionBindingsChanged()
    {
        OnPropertyChanged(nameof(SelectedRestartBehaviorOption));
        OnPropertyChanged(nameof(SelectedDownloadLocationOption));
        OnPropertyChanged(nameof(SelectedThemeOption));
        OnPropertyChanged(nameof(SelectedLanguageOption));
        OnPropertyChanged(nameof(SelectedScheduledUpdateFrequencyOption));
        OnPropertyChanged(nameof(SelectedScheduledUpdateHourOption));
        OnPropertyChanged(nameof(SelectedScheduledUpdateDayOption));
        OnPropertyChanged(nameof(IsCustomDownloadFolderVisible));
        OnPropertyChanged(nameof(IsDeveloperModeEnabled));
        OnPropertyChanged(nameof(AreScheduledUpdateControlsEnabled));
        OnPropertyChanged(nameof(IsScheduledUpdateWeeklyVisible));
        OnPropertyChanged(nameof(ScheduledUpdatesLastRunText));
        OnPropertyChanged(nameof(ScheduledUpdatesNextRunText));
        OnPropertyChanged(nameof(ScheduledUpdateTaskSchedulerStatusText));
    }

    private void ApplyTheme()
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = Settings.Theme switch
        {
            AppSettings.ThemeLight => ThemeVariant.Light,
            AppSettings.ThemeSystem => ThemeVariant.Default,
            _ => ThemeVariant.Dark
        };
    }

    private void LogSettingChanged(string propertyName)
    {
        var property = typeof(AppSettings).GetProperty(propertyName);
        var value = property?.GetValue(Settings);
        _loggingService.LogInfo($"Setting changed: {propertyName}={value}");
    }

    private void ScheduleSettingsSave()
    {
        _settingsSaveCts?.Cancel();
        _settingsSaveCts?.Dispose();

        _settingsSaveCts = new CancellationTokenSource();
        var cancellationToken = _settingsSaveCts.Token;
        _ = PersistSettingsAsync(cancellationToken);
    }

    private async Task PersistSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(200, cancellationToken);
            await _settingsService.SaveSettingsAsync(Settings.Clone(), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // A newer settings change superseded this save request.
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to save settings: {ex.Message}");
        }
    }

    private void DetectInstalledAppsNow()
    {
        if (_apps.Count == 0 || _currentPlatformId == PlatformService.Unknown)
        {
            return;
        }

        var appSource = _apps.Count > 0 ? _apps.ToList() : new List<AppItem>();
        if (appSource.Count == 0)
        {
            return;
        }

        _ = RunInstalledAppDetectionAsync(appSource);
    }

    private async Task ResetSettingsAsync()
    {
        var defaults = await _settingsService.ResetSettingsAsync();

        _suppressSettingsLogging = true;
        try
        {
            Settings.ApplyFrom(defaults);
        }
        finally
        {
            _suppressSettingsLogging = false;
        }

        ApplyTheme();
        NotifySettingsOptionBindingsChanged();
        if (Settings.AutoDetectInstalledAppsOnStartup && !_installedAppsDetected)
        {
            DetectInstalledAppsNow();
        }
        ApplyVisibilityFilter();
        StatusText = "Nova settings reset to defaults.";
        _loggingService.LogInfo(StatusText);
    }

    private void ApplyVisibilityFilter()
    {
        var query = SearchText.Trim();
        _visibleApps.Clear();

        foreach (var app in _apps)
        {
            if (app.IsHidden)
            {
                continue;
            }

            var includeByPlatform = !Settings.OsSupportedApps || app.IsSupportedOnCurrentPlatform || app.IsSelected;
            if (!includeByPlatform)
            {
                continue;
            }

            var includeByInstallState = Settings.ShowAlreadyInstalledApps || !app.IsInstalled || app.IsSelected;
            if (!includeByInstallState)
            {
                continue;
            }

            if (!MatchesSelectedFilter(app))
            {
                continue;
            }

            if (!MatchesCurrentSection(app))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var tags = app.Tags.Count == 0 ? string.Empty : string.Join(' ', app.Tags);
                var updateTerms = app.HasUpdateAvailable ? " update updates outdated newer-version" : string.Empty;
                var blob = $"{app.Name} {app.Category} {app.PublisherName} {app.Description} {app.Version} {app.InstalledVersion} {app.StatusBadge} {tags}{updateTerms}";
                if (!blob.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            _visibleApps.Add(app);
        }

        OnPropertyChanged(nameof(VisibleAppCount));
        OnPropertyChanged(nameof(SelectedFooterText));
        OnPropertyChanged(nameof(SelectedAppsCount));
        OnPropertyChanged(nameof(SelectedAppsSizeMB));
        OnPropertyChanged(nameof(SelectedAppsTimeMins));
    }

    private void SetCategoryFilter(string filter)
    {
        if (string.Equals(SelectedFilter, filter, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SelectedFilter = filter;
        _updatingFilterFlags = true;
        try
        {
            IsAllFilter = string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase);
            IsGamesFilter = string.Equals(filter, "Games", StringComparison.OrdinalIgnoreCase);
            IsDriversFilter = string.Equals(filter, "Drivers", StringComparison.OrdinalIgnoreCase);
            IsRecommendedFilter = string.Equals(filter, "Recommended", StringComparison.OrdinalIgnoreCase);
            IsDevToolsFilter = string.Equals(filter, "Dev Tools", StringComparison.OrdinalIgnoreCase);
            IsUtilitiesFilter = string.Equals(filter, "Utilities", StringComparison.OrdinalIgnoreCase);
            IsArm64Filter = string.Equals(filter, "ARM64", StringComparison.OrdinalIgnoreCase);
            IsUpdatesFilter = string.Equals(filter, "Updates", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            _updatingFilterFlags = false;
        }

        _loggingService.LogInfo($"Category filter set to '{SelectedFilter}'.");
        ApplyVisibilityFilter();
    }

    private void ShowUpdatesFilter()
    {
        if (!string.Equals(_currentSection, SectionApps, StringComparison.Ordinal))
        {
            NavigateTo(SectionApps);
        }

        SetCategoryFilter("Updates");
    }

    private void ShowRecommendedFilter()
    {
        if (!string.Equals(_currentSection, SectionApps, StringComparison.Ordinal))
        {
            NavigateTo(SectionApps);
        }

        SetCategoryFilter("Recommended");
    }

    private bool MatchesSelectedFilter(AppItem app)
    {
        if (string.Equals(SelectedFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var category = app.Category ?? string.Empty;
        return SelectedFilter switch
        {
            "Games" => category.Contains("gaming", StringComparison.OrdinalIgnoreCase),
            "Drivers" => IsDriverCategory(category),
            "Recommended" => app.IsRecommended,
            "Dev Tools" => category.Contains("coding", StringComparison.OrdinalIgnoreCase) ||
                           category.Contains("dev", StringComparison.OrdinalIgnoreCase),
            "Utilities" => category.Contains("util", StringComparison.OrdinalIgnoreCase),
            "ARM64" => app.WindowsInstall?.HasArm64Support == true,
            "Updates" => app.HasUpdateAvailable,
            _ => true
        };
    }

    private bool MatchesCurrentSection(AppItem app)
    {
        var category = app.Category ?? string.Empty;

        return _currentSection switch
        {
            SectionDrivers => IsDriverCategory(category),
            SectionMyLists => app.IsSelected,
            _ => true
        };
    }

    private static bool IsDriverCategory(string category) =>
        category.Contains("driver", StringComparison.OrdinalIgnoreCase) ||
        category.Contains("accessor", StringComparison.OrdinalIgnoreCase);

    private void NotifyScreenStateChanged()
    {
        OnPropertyChanged(nameof(IsHomeScreenVisible));
        OnPropertyChanged(nameof(IsInstallScreenVisible));
        OnPropertyChanged(nameof(IsUpdatesScreenVisible));
        OnPropertyChanged(nameof(IsHistoryScreenVisible));
        OnPropertyChanged(nameof(IsSummaryScreenVisible));
        OnPropertyChanged(nameof(IsHistoryEmptyScreenVisible));
        OnPropertyChanged(nameof(IsLogsScreenVisible));
        OnPropertyChanged(nameof(IsAboutScreenVisible));
        OnPropertyChanged(nameof(IsSelectionPhase));
    }

    private void NotifyNavigationStateChanged()
    {
        OnPropertyChanged(nameof(HomeTitle));
        OnPropertyChanged(nameof(HomeSubtitle));
        OnPropertyChanged(nameof(IsDashboardSelected));
        OnPropertyChanged(nameof(IsDashboardUnselected));
        OnPropertyChanged(nameof(IsAppsSelected));
        OnPropertyChanged(nameof(IsAppsUnselected));
        OnPropertyChanged(nameof(IsDriversSelected));
        OnPropertyChanged(nameof(IsDriversUnselected));
        OnPropertyChanged(nameof(IsMyListsSelected));
        OnPropertyChanged(nameof(IsMyListsUnselected));
        OnPropertyChanged(nameof(IsUpdatesSelected));
        OnPropertyChanged(nameof(IsUpdatesUnselected));
        OnPropertyChanged(nameof(IsHistorySelected));
        OnPropertyChanged(nameof(IsHistoryUnselected));
        OnPropertyChanged(nameof(IsLogsSelected));
        OnPropertyChanged(nameof(IsLogsUnselected));
        OnPropertyChanged(nameof(IsAboutSelected));
        OnPropertyChanged(nameof(IsAboutUnselected));
        NotifyScreenStateChanged();
        ApplyVisibilityFilter();
    }

    private void NavigateTo(string section)
    {
        if (string.Equals(_currentSection, section, StringComparison.Ordinal))
        {
            if (!IsInstalling && IsQueueVisible)
            {
                IsQueueVisible = false;
            }

            return;
        }

        if (!IsInstalling && IsQueueVisible)
        {
            IsQueueVisible = false;
        }

        _currentSection = section;
        IsSettingsPanelOpen = false;
        IsAccountMenuOpen = false;
        CloseAppDetails(logAction: false);

        _loggingService.LogInfo($"Navigation changed to '{section}'.");

        if (section == SectionHistory)
        {
            StatusText = "Downloads page selected. Latest install activity and full history are available.";
            _ = HistoryViewModel.RefreshAsync();
        }
        else if (section == SectionLogs)
        {
            StatusText = "Logs page selected. Use Open Log File to inspect the current log.";
        }
        else if (section == SectionAbout)
        {
            StatusText = $"About page selected. Running {AboutVersionText}.";
        }
        else if (section == SectionUpdates)
        {
            StatusText = UpdatesViewModel.SummaryText;
        }

        NotifyNavigationStateChanged();
    }

    private void NavigateToDriversFilter()
    {
        if (!string.Equals(_currentSection, SectionApps, StringComparison.Ordinal))
        {
            NavigateTo(SectionApps);
        }

        SetCategoryFilter("Drivers");
    }

    private void StartBackgroundStartupDetection()
    {
        if (_apps.Count == 0 || string.Equals(_currentPlatformId, PlatformService.Unknown, StringComparison.Ordinal))
        {
            return;
        }

        var selectionSnapshot = _apps.ToDictionary(app => app.Id, app => app.IsSelected, StringComparer.OrdinalIgnoreCase);
        _ = RunBackgroundStartupDetectionAsync(_apps.ToList(), selectionSnapshot);
    }

    private async Task RunBackgroundStartupDetectionAsync(
        IReadOnlyList<AppItem> appSnapshot,
        IReadOnlyDictionary<string, bool> selectionSnapshot)
    {
        _startupDetectionCts?.Cancel();
        _startupDetectionCts?.Dispose();

        var detectionCts = new CancellationTokenSource();
        _startupDetectionCts = detectionCts;
        var cancellationToken = detectionCts.Token;

        try
        {
            IReadOnlyDictionary<string, InstalledAppState> installedApps =
                new Dictionary<string, InstalledAppState>(StringComparer.OrdinalIgnoreCase);
            if (Settings.AutoDetectInstalledAppsOnStartup)
            {
                installedApps = await Task.Run(
                    () => _detectionService.DetectInstalledAppStates(appSnapshot, _currentPlatformId),
                    cancellationToken);
            }

            var hardwareDetection = await Task.Run(
                () => _detectionService.DetectHardware(_currentPlatformId),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ApplyBackgroundDetectionResults(installedApps, hardwareDetection, selectionSnapshot);
            });
        }
        catch (OperationCanceledException)
        {
            _loggingService.LogInfo("Background startup detection was cancelled.");
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Background startup detection failed: {ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText = BuildLoadedStatusText(detectionPending: false);
            });
        }
        finally
        {
            if (ReferenceEquals(_startupDetectionCts, detectionCts))
            {
                _startupDetectionCts = null;
            }

            detectionCts.Dispose();
        }
    }

    private void ApplyBackgroundDetectionResults(
        IReadOnlyDictionary<string, InstalledAppState> installedApps,
        HardwareDetectionResult hardwareDetection,
        IReadOnlyDictionary<string, bool> selectionSnapshot)
    {
        if (Settings.AutoDetectInstalledAppsOnStartup)
        {
            ApplyInstalledAppResults(installedApps);
        }

        _suppressAppSelectionHandling = true;
        try
        {
            var recommendationSummary = _detectionService.ApplyRecommendations(
                _apps,
                _currentPlatformId,
                hardwareDetection,
                autoSelectSupportedApps: false);

            var autoSelectedCount = ApplyRecommendedSelections(selectionSnapshot);

            foreach (var app in _apps)
            {
                ApplyPlatformFlags(app);
            }

            if (recommendationSummary.RecommendedAppIds.Count > 0)
            {
                _loggingService.LogInfo(
                    $"Recommendations applied in background. Total={recommendationSummary.RecommendedAppIds.Count}, Supported={recommendationSummary.SupportedRecommendations}, Unsupported={recommendationSummary.UnsupportedRecommendations}, AutoSelectedLater={autoSelectedCount}");
            }
            else
            {
                _loggingService.LogInfo("No recommendation matches found in catalog for detected hardware/accessories.");
            }
        }
        finally
        {
            _suppressAppSelectionHandling = false;
        }

        ApplyVisibilityFilter();
        NotifyAppSummaryStateChanged();
        UpdateCommandStates();
        _ = RefreshAvailableAppUpdatesAsync(rerunInstalledDetection: false, updateStatusText: false);

        if (Settings.SaveProfilesAutomatically)
        {
            SaveCurrentList(showStatusMessage: false);
            _ = _selectionService.SaveAutoProfileAsync(GetSelectedAppIds());
            _ = RefreshSavedProfilesAsync();
        }

        StatusText = BuildLoadedStatusText(detectionPending: false);
        _loggingService.LogInfo(StatusText);
    }

    private async Task RunInstalledAppDetectionAsync(IReadOnlyList<AppItem> appSnapshot)
    {
        StatusText = $"{BuildLoadedStatusText(detectionPending: false)} Scanning installed apps...";

        try
        {
            var installedApps = await Task.Run(
                () => _detectionService.DetectInstalledAppStates(appSnapshot, _currentPlatformId));

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ApplyInstalledAppResults(installedApps);
                ApplyVisibilityFilter();
                NotifyAppSummaryStateChanged();
                StatusText = BuildLoadedStatusText(detectionPending: false);
            });

            await RefreshAvailableAppUpdatesAsync(rerunInstalledDetection: false, updateStatusText: false);
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Installed app scan failed: {ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText = BuildLoadedStatusText(detectionPending: false);
            });
        }
    }

    private void ApplyInstalledAppResults(IReadOnlyDictionary<string, InstalledAppState> installedApps)
    {
        foreach (var app in _apps)
        {
            if (installedApps.TryGetValue(app.Id, out var state))
            {
                app.IsInstalled = state.IsInstalled;
                app.InstalledVersion = state.InstalledVersion;
            }
            else
            {
                app.IsInstalled = false;
                app.InstalledVersion = string.Empty;
            }

            ApplyPlatformFlags(app);
        }

        _installedAppsDetected = true;
    }

    private int ApplyRecommendedSelections(IReadOnlyDictionary<string, bool> selectionSnapshot)
    {
        var autoSelectedCount = 0;

        foreach (var app in _apps.Where(app => app.IsRecommended && app.IsSupportedOnCurrentPlatform))
        {
            var wasSelectedAtDetectionStart = selectionSnapshot.TryGetValue(app.Id, out var selected) && selected;
            if (wasSelectedAtDetectionStart || app.IsSelected)
            {
                continue;
            }

            app.IsSelected = true;
            if (!string.IsNullOrWhiteSpace(app.RecommendationReason))
            {
                app.RecommendationReason = $"{app.RecommendationReason} Auto-selected for convenience.";
            }

            autoSelectedCount++;
        }

        return autoSelectedCount;
    }

    private string BuildLoadedStatusText(bool detectionPending)
    {
        var skippedCount = _apps.Count(app => app.WillBeSkipped);
        var recommendedCount = _apps.Count(app => app.IsRecommended);

        var summary = skippedCount > 0
            ? $"Loaded {_apps.Count} apps for {CurrentPlatform}. Recommended: {recommendedCount}. {skippedCount} selected app(s) are unsupported and will be skipped."
            : $"Loaded {_apps.Count} apps for {CurrentPlatform}. Recommended: {recommendedCount}.";

        return detectionPending
            ? $"{summary} Scanning system in background..."
            : summary;
    }

    private void SaveCurrentList(bool showStatusMessage = true)
    {
        var selection = new SelectionConfig
        {
            ProfileName = CurrentProfileName,
            TargetPlatform = _currentPlatformId,
            SelectedApps = _apps.Where(app => app.IsSelected).Select(app => app.Id).ToList(),
            Settings = new SelectionSettings
            {
                SilentInstall = Settings.SilentInstall,
                SelfDelete = Settings.SelfDeleteAfterInstall,
                OsSupportedApps = Settings.OsSupportedApps,
                DefaultInstallLocation = Settings.CustomDownloadFolder
            }
        };

        _selectionService.SaveSelection(selection);
        if (showStatusMessage)
        {
            StatusText = $"Saved list '{selection.ProfileName}' with {selection.SelectedApps.Count} app(s).";
            _loggingService.LogInfo(StatusText);
        }
        else
        {
            _loggingService.LogInfo($"Auto-saved list '{selection.ProfileName}' with {selection.SelectedApps.Count} app(s).");
        }
    }

    private void BeginSaveProfile()
    {
        PendingProfileName = string.IsNullOrWhiteSpace(CurrentProfileName) ? "My Setup" : CurrentProfileName;
        PendingProfileDescription = string.Empty;
        IsSaveProfilePanelOpen = true;
        StatusText = "Enter a profile name and save the current selection.";
    }

    private void CancelSaveProfile()
    {
        IsSaveProfilePanelOpen = false;
        PendingProfileName = string.Empty;
        PendingProfileDescription = string.Empty;
        StatusText = "Profile save cancelled.";
    }

    private async Task ConfirmSaveProfileAsync()
    {
        var profileName = NormalizeProfileNameForUi(PendingProfileName);
        var description = PendingProfileDescription.Trim();
        var selectedIds = GetSelectedAppIds().ToList();

        await _profileService.SaveProfileAsync(profileName, selectedIds, description);
        await RefreshSavedProfilesAsync(selectProfileName: profileName);
        CurrentProfileName = profileName;
        IsSaveProfilePanelOpen = false;
        PendingProfileName = string.Empty;
        PendingProfileDescription = string.Empty;

        StatusText = $"Saved profile '{profileName}' with {selectedIds.Count} app(s).";
        _loggingService.LogInfo(StatusText);
    }

    private async Task LoadProfileAsync()
    {
        var storageProvider = GetStorageProvider();
        if (storageProvider is null)
        {
            StatusText = "Unable to load profiles on this platform.";
            _loggingService.LogWarning("Profile load failed because no storage provider was available.");
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Nova Profile",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Nova profiles")
                {
                    Patterns = ["*.nova"],
                    MimeTypes = ["application/json"]
                }
            ]
        });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        var localPath = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
        {
            StatusText = "Selected profile could not be opened from this location.";
            _loggingService.LogWarning($"Profile load skipped because '{file.Name}' did not expose a local file path.");
            return;
        }

        var profile = await _profileService.LoadProfileAsync(localPath);
        if (profile is null)
        {
            StatusText = $"Could not load profile from {Path.GetFileName(localPath)}.";
            return;
        }

        ApplyLoadedProfile(profile);
    }

    private async Task LoadSelectedSavedProfileAsync()
    {
        if (SelectedSavedProfile is null)
        {
            return;
        }

        ApplyLoadedProfile(SelectedSavedProfile);
        await RefreshSavedProfilesAsync(selectProfileName: SelectedSavedProfile.ProfileName);
    }

    private async Task ExportProfileAsync()
    {
        var storageProvider = GetStorageProvider();
        if (storageProvider is null)
        {
            StatusText = "Unable to export profiles on this platform.";
            _loggingService.LogWarning("Profile export failed because no storage provider was available.");
            return;
        }

        var profileName = NormalizeProfileNameForUi(CurrentProfileName);
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Nova Profile",
            SuggestedFileName = profileName,
            DefaultExtension = "nova",
            FileTypeChoices =
            [
                new FilePickerFileType("Nova profiles")
                {
                    Patterns = ["*.nova"],
                    MimeTypes = ["application/json"]
                }
            ]
        });

        if (file is null)
        {
            return;
        }

        var localPath = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
        {
            StatusText = "Selected export location is not writable from Nova.";
            _loggingService.LogWarning($"Profile export skipped because '{file.Name}' did not expose a local file path.");
            return;
        }

        var selectedIds = GetSelectedAppIds().ToList();
        await _profileService.ExportProfileAsync(profileName, selectedIds, localPath);
        StatusText = $"Exported profile '{profileName}' to {Path.GetFileName(localPath)}.";
        _loggingService.LogInfo(StatusText);
    }

    private async Task DeleteSelectedSavedProfileAsync()
    {
        if (SelectedSavedProfile is null)
        {
            return;
        }

        var profileName = SelectedSavedProfile.ProfileName;
        await _profileService.DeleteProfileAsync(profileName);
        await RefreshSavedProfilesAsync();

        if (string.Equals(CurrentProfileName, profileName, StringComparison.OrdinalIgnoreCase))
        {
            CurrentProfileName = "Website Starter";
        }

        StatusText = $"Deleted profile '{profileName}'.";
        _loggingService.LogInfo(StatusText);
    }

    private async Task RefreshSavedProfilesAsync(string? selectProfileName = null)
    {
        var profiles = await _profileService.GetSavedProfilesAsync();
        var orderedProfiles = profiles
            .OrderByDescending(profile => TryParseProfileDate(profile.CreatedOn))
            .ThenBy(profile => profile.ProfileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _savedProfiles.Clear();
        foreach (var profile in orderedProfiles)
        {
            _savedProfiles.Add(profile);
        }

        var selectedProfile = !string.IsNullOrWhiteSpace(selectProfileName)
            ? _savedProfiles.FirstOrDefault(profile =>
                string.Equals(profile.ProfileName, selectProfileName, StringComparison.OrdinalIgnoreCase))
            : SelectedSavedProfile is not null
                ? _savedProfiles.FirstOrDefault(profile =>
                    string.Equals(profile.ProfileName, SelectedSavedProfile.ProfileName, StringComparison.OrdinalIgnoreCase))
                : null;

        SelectedSavedProfile = selectedProfile ?? _savedProfiles.FirstOrDefault();
    }

    private void ApplyLoadedProfile(NovaProfile profile)
    {
        var targetIds = new HashSet<string>(
            profile.SelectedAppIds.Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.OrdinalIgnoreCase);
        var existingIds = new HashSet<string>(_apps.Select(app => app.Id), StringComparer.OrdinalIgnoreCase);
        var matchingIds = targetIds.Where(existingIds.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);

        _suppressAppSelectionHandling = true;
        try
        {
            foreach (var app in _apps)
            {
                app.IsSelected = matchingIds.Contains(app.Id);
                ApplyPlatformFlags(app);
            }
        }
        finally
        {
            _suppressAppSelectionHandling = false;
        }

        CurrentProfileName = string.IsNullOrWhiteSpace(profile.ProfileName) ? "My Setup" : profile.ProfileName;
        IsSaveProfilePanelOpen = false;
        PendingProfileName = string.Empty;
        PendingProfileDescription = string.Empty;
        SelectedSavedProfile = _savedProfiles.FirstOrDefault(savedProfile =>
            string.Equals(savedProfile.ProfileName, CurrentProfileName, StringComparison.OrdinalIgnoreCase));
        ApplyVisibilityFilter();
        NotifyAppSummaryStateChanged();
        UpdateCommandStates();
        SaveCurrentList(showStatusMessage: false);
        _ = _selectionService.SaveAutoProfileAsync(matchingIds);
        _ = RefreshSavedProfilesAsync(selectProfileName: CurrentProfileName);

        if (!string.IsNullOrWhiteSpace(profile.Platform) &&
            !string.Equals(profile.Platform, _currentPlatformId, StringComparison.OrdinalIgnoreCase))
        {
            _loggingService.LogWarning($"Profile was created on {profile.Platform} — some apps may not be available on {_currentPlatformId}");
        }

        var missingCount = targetIds.Count - matchingIds.Count;
        if (missingCount > 0)
        {
            _loggingService.LogWarning($"Profile '{CurrentProfileName}' contained {missingCount} app(s) not present in the current catalog.");
        }

        var message = $"Profile loaded: {CurrentProfileName} ({matchingIds.Count} apps selected)";
        StatusText = message;
        _loggingService.LogInfo(message);
    }

    private IEnumerable<string> GetSelectedAppIds()
    {
        return _apps
            .Where(app => app.IsSelected)
            .Select(app => app.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeProfileNameForUi(string? profileName)
    {
        var value = string.IsNullOrWhiteSpace(profileName) ? "My Setup" : profileName.Trim();
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidCharacter, '_');
        }

        return string.IsNullOrWhiteSpace(value) ? "My Setup" : value;
    }

    private static DateTimeOffset TryParseProfileDate(string? value)
    {
        return DateTimeOffset.TryParse(value, out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;
    }

    private IStorageProvider? GetStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is not Window window)
        {
            return null;
        }

        return TopLevel.GetTopLevel(window)?.StorageProvider;
    }

    private void ToggleSettingsPanel()
    {
        IsSettingsPanelOpen = !IsSettingsPanelOpen;
        if (IsSettingsPanelOpen)
        {
            IsAccountMenuOpen = false;
            CloseAppDetails(logAction: false);
        }

        _loggingService.LogInfo($"Settings panel {(IsSettingsPanelOpen ? "opened" : "closed")}.");
    }

    private void ToggleAccountMenu()
    {
        IsAccountMenuOpen = !IsAccountMenuOpen;
        if (IsAccountMenuOpen)
        {
            IsSettingsPanelOpen = false;
        }

        _loggingService.LogInfo($"Account menu {(IsAccountMenuOpen ? "opened" : "closed")}.");
    }

    private void OpenAccountProfile()
    {
        IsAccountMenuOpen = false;
        NavigateTo(SectionMyLists);
    }

    private void OpenAccountSettings()
    {
        IsAccountMenuOpen = false;
        IsSettingsPanelOpen = true;
        _loggingService.LogInfo("Settings opened from account menu.");
    }

    private void OpenAboutGitHub()
    {
        if (!_browserService.OpenUrl(NovaGitHubUrl))
        {
            StatusText = "Unable to open the Nova GitHub repository.";
        }
    }

    private async Task CheckForUpdatesAsync(bool forceManualCheck)
    {
        if (!forceManualCheck && !Settings.CheckForUpdatesAutomatically)
        {
            return;
        }

        var result = await Task.Run(async () =>
        {
            var updateCheckerService = new UpdateCheckerService(_loggingService);
            return await updateCheckerService.CheckForUpdateAsync();
        });

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!string.IsNullOrWhiteSpace(result.LatestVersion))
            {
                AboutRemoteVersionText = $"Latest available: Nova v{result.LatestVersion}";
            }

            if (result.UpdateAvailable)
            {
                IsUpdateAvailable = true;
                UpdateVersionText = $"Nova v{result.LatestVersion} is available";
                UpdateDownloadUrl = string.IsNullOrWhiteSpace(result.DownloadUrl)
                    ? result.ReleaseNotesUrl
                    : result.DownloadUrl;
                AboutUpdateStatusText = string.Empty;
                return;
            }

        if (forceManualCheck)
        {
            AboutUpdateStatusText = "Nova is up to date";
            if (string.IsNullOrWhiteSpace(result.LatestVersion))
            {
                AboutRemoteVersionText = "Latest available: Unknown";
            }
        }
    });
}

    private async Task CheckForAppUpdatesAsync()
    {
        UpdatesViewModel.IsBusy = true;
        try
        {
            await RefreshAvailableAppUpdatesAsync(rerunInstalledDetection: true, updateStatusText: true);
        }
        finally
        {
            UpdatesViewModel.IsBusy = false;
        }
    }

    public bool IsArm64Machine => PlatformService.IsArm64();

    private bool CanRunScheduledUpdatesNow()
    {
        return !IsInstalling &&
               Settings.ScheduledUpdatesEnabled &&
               _scheduledUpdateService is not null;
    }

    private async Task RunScheduledUpdatesNowAsync()
    {
        if (_scheduledUpdateService is null)
        {
            return;
        }

        StatusText = "Running scheduled updates now...";
        try
        {
            await _scheduledUpdateService.RunScheduledUpdateAsync();
            StatusText = "Scheduled update run finished.";
        }
        catch (Exception ex)
        {
            StatusText = "Scheduled update run failed.";
            _loggingService.LogError($"Scheduled update run failed: {ex.Message}");
        }
        finally
        {
            OnPropertyChanged(nameof(ScheduledUpdatesLastRunText));
            OnPropertyChanged(nameof(ScheduledUpdatesNextRunText));
            OnPropertyChanged(nameof(ScheduledUpdateTaskSchedulerStatusText));
        }
    }

    private async Task UpdateAllAppsAsync()
    {
        if (!UpdatesViewModel.HasUpdates)
        {
            return;
        }

        UpdatesViewModel.IsBusy = true;
        try
        {
            await _appUpdateService.UpdateAllAsync(UpdatesViewModel.AvailableUpdates.ToList(), _installerService);
            await HistoryViewModel.RefreshAsync();
            await RefreshAvailableAppUpdatesAsync(rerunInstalledDetection: true, updateStatusText: true);
        }
        finally
        {
            UpdatesViewModel.IsBusy = false;
        }
    }

    private async Task UpdateSingleAppAsync(AppItem app)
    {
        if (app is null)
        {
            return;
        }

        UpdatesViewModel.IsBusy = true;
        try
        {
            await _appUpdateService.UpdateAppAsync(app, _installerService);
            await HistoryViewModel.RefreshAsync();
            await RefreshAvailableAppUpdatesAsync(rerunInstalledDetection: true, updateStatusText: true);
        }
        finally
        {
            UpdatesViewModel.IsBusy = false;
        }
    }

    private async Task RefreshAvailableAppUpdatesAsync(bool rerunInstalledDetection, bool updateStatusText)
    {
        if (string.Equals(_currentPlatformId, PlatformService.Unknown, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            if (rerunInstalledDetection)
            {
                var installedSnapshot = _apps.ToList();
                var installedApps = await Task.Run(() =>
                    _detectionService.DetectInstalledAppStates(installedSnapshot, _currentPlatformId));

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ApplyInstalledAppResults(installedApps);
                    ApplyVisibilityFilter();
                    NotifyAppSummaryStateChanged();
                    NotifyAppDetailsStateChanged();
                });
            }

            var latestVersions = await Task.Run(() =>
                _appUpdateService.ResolveLatestCatalogVersions(_apps.ToList(), _currentPlatformId));

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ApplyResolvedCatalogVersions(latestVersions);
                RefreshAllAppVisualStates();

                var availableUpdates = _appUpdateService.GetAppsWithUpdates(_apps.ToList());
                UpdatesViewModel.SetAvailableUpdates(availableUpdates);
                NotifyAppSummaryStateChanged();
                NotifyAppDetailsStateChanged();
                UpdateCommandStates();

                if (updateStatusText)
                {
                    StatusText = UpdatesViewModel.SummaryText;
                }
            });
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"App update check failed: {ex.Message}");
        }
    }

    private void ApplyResolvedCatalogVersions(IReadOnlyDictionary<string, string> latestVersions)
    {
        if (latestVersions is null || latestVersions.Count == 0)
        {
            return;
        }

        foreach (var app in _apps)
        {
            if (!latestVersions.TryGetValue(app.Id, out var latestVersion) ||
                string.IsNullOrWhiteSpace(latestVersion))
            {
                continue;
            }

            app.Version = latestVersion;
        }
    }

    private void DismissUpdateBanner()
    {
        IsUpdateAvailable = false;
    }

    private void DownloadUpdate()
    {
        if (!_browserService.OpenUrl(UpdateDownloadUrl))
        {
            StatusText = "Unable to open the Nova download page.";
        }
    }

    private async Task RunStartupDetectionInlineAsync(Func<string, Task>? updateStatusAsync)
    {
        if (_apps.Count == 0 || string.Equals(_currentPlatformId, PlatformService.Unknown, StringComparison.Ordinal))
        {
            StatusText = BuildLoadedStatusText(detectionPending: false);
            _loggingService.LogInfo(StatusText);

            if (updateStatusAsync is not null)
            {
                await updateStatusAsync("Almost ready...");
            }

            return;
        }

        var selectionSnapshot = _apps.ToDictionary(app => app.Id, app => app.IsSelected, StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, InstalledAppState> installedApps =
            new Dictionary<string, InstalledAppState>(StringComparer.OrdinalIgnoreCase);

        if (updateStatusAsync is not null)
        {
            await updateStatusAsync("Detecting installed apps...");
        }

        if (Settings.AutoDetectInstalledAppsOnStartup)
        {
            installedApps = await Task.Run(() =>
                _detectionService.DetectInstalledAppStates(_apps.ToList(), _currentPlatformId));
        }

        var hardwareDetection = await Task.Run(() => _detectionService.DetectHardware(_currentPlatformId));
        ApplyBackgroundDetectionResults(installedApps, hardwareDetection, selectionSnapshot);

        if (updateStatusAsync is not null)
        {
            await updateStatusAsync("Almost ready...");
        }
    }

    public void CloseAccountMenuOverlay()
    {
        if (!IsAccountMenuOpen)
        {
            return;
        }

        IsAccountMenuOpen = false;
        _loggingService.LogInfo("Account menu closed.");
    }

    private void OpenPublisherHomepage(object? parameter)
    {
        if (parameter is string url)
        {
            if (!_browserService.OpenUrl(url))
            {
                StatusText = "Could not open link.";
            }

            return;
        }

        if (parameter is not AppItem app)
        {
            return;
        }

        if (!_browserService.OpenPublisherHomepage(app.HomepageUrl))
        {
            StatusText = $"Could not open homepage for {app.Name}.";
        }
    }

    private bool CanExecuteAppContextParameter(object? parameter)
    {
        return !IsInstalling && parameter is AppItem;
    }

    private bool CanExecuteAppContextInstall(object? parameter)
    {
        return !IsInstalling && parameter is AppItem app && !app.IsInstalled;
    }

    private bool CanExecuteAppContextUpdate(object? parameter)
    {
        return !IsInstalling && parameter is AppItem app && app.HasUpdateAvailable;
    }

    private bool CanExecuteAppContextUninstall(object? parameter)
    {
        return !IsInstalling && parameter is AppItem app && app.IsInstalled;
    }

    private bool CanExecuteAppContextInstalledAction(object? parameter)
    {
        return !IsInstalling && parameter is AppItem app && app.IsInstalled;
    }

    private void InstallAppFromContextMenu(object? parameter)
    {
        if (parameter is not AppItem app)
        {
            return;
        }

        _ = InstallAppFromContextMenuAsync(app);
    }

    private async Task InstallAppFromContextMenuAsync(AppItem app)
    {
        try
        {
            await InstallAppsAsync(new[] { app });
        }
        catch (Exception ex)
        {
            StatusText = $"Install failed to start for {app.Name}.";
            _loggingService.LogError($"Context-menu install failed for {app.Name}: {ex.Message}");
        }
    }

    private void UpdateAppFromContextMenu(object? parameter)
    {
        if (parameter is not AppItem app)
        {
            return;
        }

        _ = UpdateAppFromContextMenuAsync(app);
    }

    private async Task UpdateAppFromContextMenuAsync(AppItem app)
    {
        try
        {
            await UpdateSingleAppAsync(app);
        }
        catch (Exception ex)
        {
            StatusText = $"Update failed for {app.Name}.";
            _loggingService.LogError($"Context-menu update failed for {app.Name}: {ex.Message}");
        }
    }

    private void UninstallAppFromContextMenu(object? parameter)
    {
        if (parameter is not AppItem app)
        {
            return;
        }

        _ = UninstallAppAsync(app);
    }

    private async Task UninstallAppAsync(AppItem app)
    {
        if (!OperatingSystem.IsWindows())
        {
            StatusText = "Uninstall is only supported on Windows.";
            _loggingService.LogWarning($"Uninstall requested for {app.Name}, but the current platform does not support winget uninstall.");
            return;
        }

        if (string.IsNullOrWhiteSpace(app.WingetId))
        {
            StatusText = $"Uninstall is not supported for {app.Name}.";
            _loggingService.LogWarning($"Uninstall requested for {app.Name}, but no WingetId is configured.");
            return;
        }

        try
        {
            StatusText = $"Uninstalling {app.Name}...";
            _loggingService.LogInfo($"Starting uninstall for {app.Name} via winget.");

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ResolveWingetExecutablePathForCommands(),
                    Arguments = $"uninstall --id {QuoteArgument(app.WingetId)} --exact --silent --disable-interactivity",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            var output = string.Join(
                Environment.NewLine,
                new[] { stdout, stderr }.Where(text => !string.IsNullOrWhiteSpace(text)));

            if (process.ExitCode == 0)
            {
                app.IsInstalled = false;
                app.InstalledVersion = string.Empty;
                app.HasInstallFailed = false;
                app.RequiresRestartHint = false;
                app.StatusBadge = AppItem.StatusNotInstalled;
                StatusText = $"{app.Name} uninstalled.";
                _loggingService.LogInfo(
                    string.IsNullOrWhiteSpace(output)
                        ? $"{app.Name} uninstalled successfully."
                        : $"{app.Name} uninstalled successfully. {output.Trim()}");
                await RefreshAvailableAppUpdatesAsync(rerunInstalledDetection: true, updateStatusText: false);
                await HistoryViewModel.RefreshAsync();
                return;
            }

            StatusText = $"Uninstall failed for {app.Name}.";
            _loggingService.LogWarning(
                string.IsNullOrWhiteSpace(output)
                    ? $"winget uninstall failed for {app.Name} with exit code {process.ExitCode}."
                    : $"winget uninstall failed for {app.Name} with exit code {process.ExitCode}. {output.Trim()}");
        }
        catch (Exception ex)
        {
            StatusText = $"Uninstall failed for {app.Name}.";
            _loggingService.LogError($"Failed to uninstall {app.Name}: {ex.Message}");
        }
    }

    private void OpenInstallLocationFromContextMenu(object? parameter)
    {
        if (parameter is not AppItem app)
        {
            return;
        }

        OpenInstallLocation(app);
    }

    private void OpenInstallLocation(AppItem app)
    {
        if (!OperatingSystem.IsWindows())
        {
            StatusText = "Open install location is only supported on Windows.";
            _loggingService.LogWarning($"Open install location requested for {app.Name}, but the current platform does not support Windows Explorer.");
            return;
        }

        var executablePath = _detectionService.TryGetInstalledExecutablePath(app, _currentPlatformId);
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            StatusText = $"Could not find an install location for {app.Name}.";
            _loggingService.LogWarning($"Open install location requested for {app.Name}, but no executable path could be resolved.");
            return;
        }

        var directoryPath = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            StatusText = $"Could not find an install folder for {app.Name}.";
            _loggingService.LogWarning($"Open install location requested for {app.Name}, but the resolved folder was unavailable: {directoryPath}");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = QuoteArgument(directoryPath),
                UseShellExecute = true
            });

            StatusText = $"Opened install location for {app.Name}.";
            _loggingService.LogInfo($"Opened install location for {app.Name}: {directoryPath}");
        }
        catch (Exception ex)
        {
            StatusText = $"Could not open install location for {app.Name}.";
            _loggingService.LogError($"Failed to open install location for {app.Name}: {ex.Message}");
        }
    }

    private void CopyToClipboardFromContextMenu(object? parameter)
    {
        if (parameter is not string text || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _ = CopyToClipboardAsync(text);
    }

    private async Task CopyToClipboardAsync(string text)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is not Window window)
        {
            StatusText = "Clipboard unavailable: operation requires desktop clipboard access.";
            _loggingService.LogWarning("Copy to clipboard failed: clipboard unavailable (no desktop application lifetime).");
            return;
        }

        var clipboard = TopLevel.GetTopLevel(window)?.Clipboard;
        if (clipboard is null)
        {
            StatusText = "Clipboard unavailable: operation requires desktop clipboard access.";
            _loggingService.LogWarning("Copy to clipboard failed: clipboard unavailable (TopLevel not available).");
            return;
        }

        await clipboard.SetTextAsync(text);
        StatusText = $"Copied '{text}' to clipboard.";
        _loggingService.LogInfo($"Copied '{text}' to clipboard.");
    }

    private void ToggleSilentInstallPreference(object? parameter)
    {
        if (parameter is not AppItem app)
        {
            return;
        }

        app.UserDisabledSilentInstall = !app.UserDisabledSilentInstall;
        _loggingService.LogInfo(
            app.UserDisabledSilentInstall
                ? $"Silent install disabled for {app.Name} by user preference."
                : $"Silent install enabled for {app.Name} by user preference.");
    }

    private void ToggleScanningPreference(object? parameter)
    {
        if (parameter is not AppItem app)
        {
            return;
        }

        app.UserDisabledScanning = !app.UserDisabledScanning;
        _loggingService.LogInfo(
            app.UserDisabledScanning
                ? $"Update scanning disabled for {app.Name} by user preference."
                : $"Update scanning enabled for {app.Name} by user preference.");
    }

    private void ToggleInstallScriptsPreference(object? parameter)
    {
        if (parameter is not AppItem app)
        {
            return;
        }

        app.UserTrustedInstallScripts = !app.UserTrustedInstallScripts;
        _loggingService.LogInfo(
            app.UserTrustedInstallScripts
                ? $"Install scripts enabled for {app.Name} by user preference."
                : $"Install scripts disabled for {app.Name} by user preference.");
    }

    private void ShowAppDetails(object? parameter)
    {
        if (parameter is not AppItem app)
        {
            return;
        }

        SelectedDetailApp = app;
        IsAppDetailsOpen = true;
        IsSettingsPanelOpen = false;
        StatusText = $"Viewing details for {app.Name}.";
        _loggingService.LogInfo($"Opened details panel for '{app.Name}'.");
    }

    private void CloseAppDetails(bool logAction = true)
    {
        if (!IsAppDetailsOpen && SelectedDetailApp is null)
        {
            return;
        }

        IsAppDetailsOpen = false;
        SelectedDetailApp = null;

        if (logAction)
        {
            _loggingService.LogInfo("Closed app details panel.");
        }
    }

    private void PauseInstallPlaceholder()
    {
        _installQueueCts?.Cancel();
        _installerService.CancelAllActiveInstalls();
        InstallStatusText = "Cancel requested for all active installs.";
        StatusText = InstallStatusText;
        _loggingService.LogInfo("Cancel requested for all active installs.");
    }

    private void ExportReportPlaceholder()
    {
        StatusText = "Export report (PDF) is a placeholder for the next step.";
        _loggingService.LogInfo("Export report requested (placeholder).");
    }

    private void OpenLogFile()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _loggingService.LogFilePath,
                UseShellExecute = true
            });

            _loggingService.LogInfo($"Opened log file: {_loggingService.LogFilePath}");
        }
        catch (Exception ex)
        {
            StatusText = "Could not open log file.";
            _loggingService.LogError($"Failed to open log file: {ex.Message}");
        }
    }

    private async Task ClearLogsAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => LoggingService.LiveLogs.Clear());
    }

    private async Task CopyLogsAsync()
    {
        var allLogs = string.Join(Environment.NewLine, FilteredLiveLogs.Select(entry => entry.DisplayText));
        if (string.IsNullOrWhiteSpace(allLogs))
        {
            return;
        }

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is not Window window)
        {
            StatusText = "Clipboard unavailable: operation requires desktop clipboard access.";
            _loggingService.LogWarning("Copy logs failed: clipboard unavailable (no desktop application lifetime).");
            return;
        }

        var clipboard = TopLevel.GetTopLevel(window)?.Clipboard;
        if (clipboard is null)
        {
            StatusText = "Clipboard unavailable: operation requires desktop clipboard access.";
            _loggingService.LogWarning("Copy logs failed: clipboard unavailable (TopLevel not available).");
            return;
        }

        await clipboard.SetTextAsync(allLogs);
        _loggingService.LogInfo("Copied developer console logs to clipboard.");
    }

    private async Task CopySelectedLogAsync()
    {
        if (SelectedLiveLogEntry is null)
        {
            return;
        }

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is not Window window)
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(window)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        await clipboard.SetTextAsync(SelectedLiveLogEntry.DisplayText);
        _loggingService.LogInfo("Copied selected developer console log entry to clipboard.");
    }

    private async Task ExportLogsAsync()
    {
        if (FilteredLiveLogs.Count == 0)
        {
            return;
        }

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is not Window window)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(window);
        var storageProvider = topLevel?.StorageProvider;
        if (storageProvider is null)
        {
            StatusText = "Unable to export developer console logs on this platform.";
            _loggingService.LogWarning("Developer console export failed because no storage provider was available.");
            return;
        }

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Developer Console Logs",
            SuggestedFileName = $"nova-developer-console-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            DefaultExtension = "txt",
            FileTypeChoices =
            [
                new FilePickerFileType("Text files")
                {
                    Patterns = ["*.txt"],
                    MimeTypes = ["text/plain"]
                }
            ]
        });

        if (file is null)
        {
            return;
        }

        try
        {
            var content = string.Join(Environment.NewLine, FilteredLiveLogs.Select(entry => entry.DisplayText));
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(content);
            await writer.FlushAsync();

            StatusText = $"Developer console exported to {file.Name}.";
            _loggingService.LogInfo($"Exported developer console logs to {file.Name}.");
        }
        catch (Exception ex)
        {
            StatusText = $"Export failed for {file.Name}: {ex.Message}";
            _loggingService.LogError($"Export logs failed for {file.Name}: {ex.Message}");
        }
    }

    private void RefreshFilteredLiveLogs()
    {
        // Take a thread-safe snapshot of LiveLogs to avoid concurrent modification exceptions
        List<LogEntry> snapshot;
        lock (LoggingService.LiveLogs)
        {
            snapshot = new List<LogEntry>(LoggingService.LiveLogs);
        }

        IEnumerable<LogEntry> source = snapshot;
        source = _selectedLogLevelFilter switch
        {
            LogFilterInfo => source.Where(entry => entry.Level == LogLevel.Info),
            LogFilterSuccess => source.Where(entry => entry.Level == LogLevel.Success),
            LogFilterWarning => source.Where(entry => entry.Level == LogLevel.Warning),
            LogFilterError => source.Where(entry => entry.Level == LogLevel.Error),
            LogFilterDebug => source.Where(entry => entry.Level == LogLevel.Debug),
            _ => source
        };

        var selectedDisplayText = SelectedLiveLogEntry?.DisplayText;

        _filteredLiveLogs.Clear();
        foreach (var entry in source)
        {
            _filteredLiveLogs.Add(entry);
        }

        if (!string.IsNullOrWhiteSpace(selectedDisplayText))
        {
            SelectedLiveLogEntry = _filteredLiveLogs.FirstOrDefault(entry =>
                string.Equals(entry.DisplayText, selectedDisplayText, StringComparison.Ordinal));
        }
        else if (SelectedLiveLogEntry is not null && !_filteredLiveLogs.Contains(SelectedLiveLogEntry))
        {
            SelectedLiveLogEntry = null;
        }

        OnPropertyChanged(nameof(FilteredLiveLogs));
        _copyLogsCommand.RaiseCanExecuteChanged();
        _copySelectedLogCommand.RaiseCanExecuteChanged();
        _exportLogsCommand.RaiseCanExecuteChanged();
    }

    private void ShowHelpPlaceholder()
    {
        StatusText = "Help and documentation are not available yet.";
        _loggingService.LogInfo(StatusText);
    }

    private async Task RefreshCatalogAsync()
    {
        if (IsInstalling || string.Equals(_currentPlatformId, PlatformService.Unknown, StringComparison.Ordinal))
        {
            return;
        }

        var selectedIds = new HashSet<string>(
            _apps.Where(app => app.IsSelected).Select(app => app.Id),
            StringComparer.OrdinalIgnoreCase);
        var detailAppId = SelectedDetailApp?.Id;
        var wasDetailsOpen = IsAppDetailsOpen;

        StatusText = "Refreshing catalog and app state...";
        _loggingService.LogInfo("Manual refresh started.");

        var refreshedApps = await _catalogService.LoadAppsAsync(_currentPlatformId);
        await _appPreferencesService.ApplyToAppsAsync(refreshedApps);
        if (refreshedApps.Count > 0)
        {
            ReplaceCatalogApps(refreshedApps, selectedIds, detailAppId, wasDetailsOpen);
        }
        else if (_apps.Count > 0)
        {
            _loggingService.LogWarning("Catalog refresh returned no apps. Keeping the current in-memory catalog.");
        }

        var appSnapshot = _apps.ToList();
        IReadOnlyDictionary<string, InstalledAppState> installedApps =
            new Dictionary<string, InstalledAppState>(StringComparer.OrdinalIgnoreCase);
        HardwareDetectionResult? hardwareDetection = null;

        try
        {
            installedApps = await Task.Run(() => _detectionService.DetectInstalledAppStates(appSnapshot, _currentPlatformId));
            hardwareDetection = await Task.Run(() => _detectionService.DetectHardware(_currentPlatformId));
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Manual refresh background scan failed: {ex.Message}");
        }

        ApplyInstalledAppResults(installedApps);

        if (hardwareDetection is not null)
        {
            _suppressAppSelectionHandling = true;
            try
            {
                _detectionService.ApplyRecommendations(
                    _apps,
                    _currentPlatformId,
                    hardwareDetection,
                    autoSelectSupportedApps: false);

                foreach (var app in _apps)
                {
                    ApplyPlatformFlags(app);
                }
            }
            finally
            {
                _suppressAppSelectionHandling = false;
            }
        }

        ApplyVisibilityFilter();
        NotifyAppSummaryStateChanged();
        NotifyAppDetailsStateChanged();
        UpdateCommandStates();
        await RefreshAvailableAppUpdatesAsync(rerunInstalledDetection: false, updateStatusText: false);

        StatusText = $"{BuildLoadedStatusText(detectionPending: false)} Refreshed current catalog and app state.";
        _loggingService.LogInfo("Manual refresh completed.");
        _loggingService.LogInfo(StatusText);
    }

    private void HandleAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressAppSelectionHandling || sender is not AppItem app)
        {
            return;
        }

        if (IsAppPreferenceProperty(e.PropertyName))
        {
            _ = SaveAppPreferenceAsync(app);
            SyncUpdatesViewFromCurrentApps();
            return;
        }

        if (e.PropertyName != nameof(AppItem.IsSelected))
        {
            return;
        }

        ApplyPlatformFlags(app);
        ApplyVisibilityFilter();
        NotifyAppSummaryStateChanged();
        UpdateCommandStates();

        if (Settings.SaveProfilesAutomatically)
        {
            SaveCurrentList(showStatusMessage: false);
        }
    }

    private static bool IsAppPreferenceProperty(string? propertyName)
    {
        return propertyName == nameof(AppItem.UserDisabledSilentInstall) ||
               propertyName == nameof(AppItem.UserDisabledScanning) ||
               propertyName == nameof(AppItem.UserDisabledAutoUpdate) ||
               propertyName == nameof(AppItem.UserTrustedInstallScripts);
    }

    private async Task SaveAppPreferenceAsync(AppItem app)
    {
        await _appPreferencesService.SavePreferenceAsync(
            app.Id,
            new AppUserPreference
            {
                DisableSilentInstall = app.UserDisabledSilentInstall,
                DisableScanning = app.UserDisabledScanning,
                DisableAutoUpdate = app.UserDisabledAutoUpdate,
                AllowInstallScripts = app.UserTrustedInstallScripts
            });
    }

    private void SyncUpdatesViewFromCurrentApps()
    {
        RefreshAllAppVisualStates();

        var availableUpdates = _apps
            .Where(app => !app.UserDisabledScanning && app.HasUpdateAvailable)
            .OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        UpdatesViewModel.SetAvailableUpdates(availableUpdates);
        NotifyAppDetailsStateChanged();
    }

    private bool CanInstall()
    {
        return !IsInstalling && _apps.Any(app => app.IsSelected);
    }

    private async Task InstallSelectedAsync()
    {
        var selectedApps = _apps.Where(app => app.IsSelected).ToList();
        if (selectedApps.Count == 0)
        {
            InstallStatusText = "No selected apps to install.";
            return;
        }

        await InstallAppsAsync(selectedApps);
    }

    private async Task InstallAppsAsync(IReadOnlyList<AppItem> selectedApps)
    {
        if (selectedApps.Count == 0)
        {
            InstallStatusText = "No selected apps to install.";
            return;
        }

        ResetInstallOutputState();
        IsSettingsPanelOpen = false;
        CloseAppDetails(logAction: false);
        IsInstalling = true;
        _ = ShowDependencyInstallInfoAsync(selectedApps);
        PrepareInstallQueue(selectedApps);

        InstallStatusText = $"Starting installation for {selectedApps.Count} app(s)...";
        _loggingService.LogInfo(InstallStatusText);
        _loggingService.LogInfo(
            $"Install settings: Silent={Settings.SilentInstall}, SelfDelete={Settings.SelfDeleteAfterInstall}, Parallel={Settings.ParallelInstall}, OsSupportedApps={Settings.OsSupportedApps}, DownloadLocationMode={Settings.DownloadLocationMode}, CustomDownloadFolder='{Settings.CustomDownloadFolder}', KeepInstallers={Settings.KeepInstallersAfterInstall}, RestartBehavior={Settings.RestartBehavior}");

        if (Settings.ParallelInstall && selectedApps.Count > 1)
        {
            _loggingService.LogInfo("Parallel install is enabled in settings, but the installer currently uses the stable sequential pipeline.");
        }

        _installQueueCts?.Cancel();
        _installQueueCts?.Dispose();
        _installQueueCts = new CancellationTokenSource();
        var installQueueToken = _installQueueCts.Token;
        var queueProgress = new Progress<InstallQueueProgress>(HandleInstallQueueProgress);

        var processedCount = 0;
        try
        {
            foreach (var app in selectedApps)
            {
                installQueueToken.ThrowIfCancellationRequested();
                InstallStatusText = $"Installing {app.Name} ({processedCount + 1}/{selectedApps.Count})...";
                var batchResults = await _installerService.InstallSelectedAppsAsync(
                    new[] { app },
                    _currentPlatformId,
                    silentInstallEnabled: Settings.SilentInstall,
                    keepInstallersAfterInstall: Settings.KeepInstallersAfterInstall,
                    downloadLocationMode: Settings.DownloadLocationMode,
                    customDownloadFolder: Settings.CustomDownloadFolder,
                    catalogApps: _apps,
                    queueProgress: queueProgress,
                    cancellationToken: installQueueToken);

                foreach (var result in batchResults)
                {
                    AddInstallResult(result);
                    ApplyInstallResultToApp(result);
                    ApplyInstallResultToQueue(result);
                }

                processedCount++;
                ProgressValue = Math.Round((double)processedCount / selectedApps.Count * 100.0, 1);

                if (installQueueToken.IsCancellationRequested)
                {
                    break;
                }
            }

            await RefreshInstalledStatesAfterInstallAsync();
            await HistoryViewModel.RefreshAsync();
            FinalizeInstallSummary();
            await ApplyPostInstallRestartBehaviorAsync();
        }
        catch (OperationCanceledException)
        {
            MarkPendingQueueItemsCancelled();
            InstallStatusText = "Installation cancelled.";
            StatusText = InstallStatusText;
            IsQueueVisible = false;
            _loggingService.LogWarning("Install queue cancelled.");
        }
        finally
        {
            _installQueueCts?.Dispose();
            _installQueueCts = null;
            ResetAppCancellationState();
            IsInstalling = false;
            UpdateCommandStates();
        }
    }

    private async Task BrowsePortableFolderAsync()
    {
        var storageProvider = GetStorageProvider();
        if (storageProvider is null)
        {
            StatusText = "Portable folder picker is unavailable on this platform.";
            return;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Choose portable apps folder"
        });

        var folder = folders.FirstOrDefault();
        var localPath = folder?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
        {
            return;
        }

        Settings.DefaultPortableFolder = localPath;
        StatusText = $"Portable apps folder set to {localPath}.";
        _loggingService.LogInfo(StatusText);
    }

    private void ResetInstallOutputState()
    {
        ProgressValue = 0;
        InstallSummaryText = string.Empty;
        RestartRequired = false;
        RestartStatusText = string.Empty;
        _restartDecisionFinalized = false;
        IsRestartConfirmationVisible = false;
        _installResults.Clear();
        _installedResults.Clear();
        _failedResults.Clear();
        _cancelledResults.Clear();
        _skippedResults.Clear();
        _restartRequiredResults.Clear();
        _unsupportedSkippedResults.Clear();
        _installQueue.Clear();
        IsQueueVisible = false;
    }

    private void PrepareInstallQueue(IEnumerable<AppItem> selectedApps)
    {
        _installQueue.Clear();
        foreach (var app in selectedApps)
        {
            _installQueue.Add(new InstallQueueItem
            {
                AppId = app.Id,
                AppName = app.Name,
                IconGlyph = string.IsNullOrWhiteSpace(app.Name)
                    ? "?"
                    : app.Name[..1].ToUpperInvariant(),
                Status = InstallQueueStatus.Pending,
                StatusText = "Waiting...",
                Progress = 0,
                IsActive = false
            });
        }

        IsQueueVisible = _installQueue.Count > 0;
    }

    private void ResetAppCancellationState()
    {
        foreach (var app in _apps)
        {
            app.IsCancellable = false;
            app.CancelCommand = null;
        }
    }

    private void CancelSingleInstall(AppItem app)
    {
        if (app is null)
        {
            return;
        }

        app.IsCancellable = false;
        app.CancelCommand = null;
        InstallStatusText = $"Cancelling {app.Name}...";
        _installerService.CancelApp(app.Id);
    }

    private void HandleInstallQueueProgress(InstallQueueProgress progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var queueItem = _installQueue.FirstOrDefault(item =>
                item.AppId.Equals(progress.AppId, StringComparison.OrdinalIgnoreCase));
            if (queueItem is null)
            {
                return;
            }

            var isActiveStatus = progress.Status == InstallQueueStatus.Downloading || progress.Status == InstallQueueStatus.Installing;
            if (isActiveStatus)
            {
                foreach (var item in _installQueue)
                {
                    item.IsActive = false;
                }
            }

            queueItem.Status = progress.Status;
            queueItem.StatusText = progress.StatusText;
            queueItem.Progress = Math.Clamp(progress.Progress, 0, 1);
            queueItem.IsActive = isActiveStatus;
            OnPropertyChanged(nameof(InstallQueueHeaderText));
        });
    }

    private void ApplyInstallResultToQueue(InstallResult result)
    {
        var queueItem = _installQueue.FirstOrDefault(item =>
            item.AppId.Equals(result.AppId, StringComparison.OrdinalIgnoreCase));
        if (queueItem is null)
        {
            return;
        }

        queueItem.Status = result switch
        {
            { Cancelled: true } => InstallQueueStatus.Cancelled,
            { Skipped: true } => InstallQueueStatus.Skipped,
            { Success: true } => InstallQueueStatus.Done,
            _ => InstallQueueStatus.Failed
        };
        queueItem.StatusText = queueItem.Status switch
        {
            InstallQueueStatus.Done => "Done ✓",
            InstallQueueStatus.Cancelled => "Cancelled",
            InstallQueueStatus.Skipped => "Skipped",
            _ => "Failed"
        };
        queueItem.Progress = 1.0;
        queueItem.IsActive = false;
        OnPropertyChanged(nameof(InstallQueueHeaderText));
    }

    private void MarkPendingQueueItemsCancelled()
    {
        foreach (var item in _installQueue.Where(item =>
                     item.Status == InstallQueueStatus.Pending ||
                     item.Status == InstallQueueStatus.Downloading ||
                     item.Status == InstallQueueStatus.Installing))
        {
            item.Status = InstallQueueStatus.Cancelled;
            item.StatusText = "Cancelled";
            item.Progress = 1.0;
            item.IsActive = false;
        }

        OnPropertyChanged(nameof(InstallQueueHeaderText));
    }

    private void ClearQueue()
    {
        if (IsInstalling)
        {
            return;
        }

        _installQueue.Clear();
        IsQueueVisible = false;
        _loggingService.LogInfo("Install queue cleared.");
    }

    private async Task ShowDependencyInstallInfoAsync(IReadOnlyCollection<AppItem> selectedApps)
    {
        var selectedIds = selectedApps
            .Where(app => !string.IsNullOrWhiteSpace(app.Id))
            .Select(app => app.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var allAppsById = _apps
            .Where(app => !string.IsNullOrWhiteSpace(app.Id))
            .GroupBy(app => app.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var missingDependencies = await Task.Run(
            () => _dependencyResolverService.GetMissingDependencies(selectedIds, allAppsById, _detectionService));

        if (missingDependencies.Count == 0)
        {
            ClearDependencyInfoBanner();
            return;
        }

        DependencyInfoText = "The following dependencies will also be installed: " +
                             string.Join(", ", missingDependencies.Select(app => app.Name));
        IsDependencyInfoVisible = true;
        _loggingService.LogInfo(DependencyInfoText);
        _ = AutoDismissDependencyInfoAsync();
    }

    private async Task AutoDismissDependencyInfoAsync()
    {
        _dependencyInfoDismissCts?.Cancel();
        _dependencyInfoDismissCts?.Dispose();

        if (!IsDependencyInfoVisible)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _dependencyInfoDismissCts = cts;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            ClearDependencyInfoBanner();
        }
        catch (OperationCanceledException)
        {
            // Intentionally ignored.
        }
        finally
        {
            if (ReferenceEquals(_dependencyInfoDismissCts, cts))
            {
                _dependencyInfoDismissCts = null;
            }

            cts.Dispose();
        }
    }

    private void ClearDependencyInfoBanner()
    {
        IsDependencyInfoVisible = false;
        DependencyInfoText = string.Empty;
    }

    private void AddInstallResult(InstallResult result)
    {
        _installResults.Add(result);

        if (result.Success)
        {
            _installedResults.Add(result);
        }
        else if (result.Cancelled)
        {
            _cancelledResults.Add(result);
        }
        else if (result.Skipped)
        {
            _skippedResults.Add(result);
            if (IsUnsupportedSkippedResult(result))
            {
                _unsupportedSkippedResults.Add(result);
            }
        }
        else
        {
            _failedResults.Add(result);
        }

        if (result.RequiresRestart)
        {
            _restartRequiredResults.Add(result);
        }
    }

    private void FinalizeInstallSummary()
    {
        var successCount = _installedResults.Count;
        var skippedCount = _skippedResults.Count;
        var failedCount = _failedResults.Count;
        var cancelledCount = _cancelledResults.Count;
        var restartCount = _restartRequiredResults.Count;

        RestartRequired = restartCount > 0;
        ProgressValue = 100;
        InstallStatusText = "Installation finished.";
        InstallSummaryText = $"Installed: {successCount} | Failed: {failedCount} | Cancelled: {cancelledCount} | Skipped: {skippedCount} | Restart: {restartCount}";
        StatusText = InstallSummaryText;

        if (Settings.SelfDeleteAfterInstall)
        {
            _loggingService.LogWarning("Self-delete is enabled, but safe placeholder mode is active. No deletion was performed.");
        }

        if (string.Equals(Settings.DownloadLocationMode, AppSettings.DownloadCustomFolder, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(Settings.CustomDownloadFolder))
        {
            _loggingService.LogInfo(
                $"Custom download folder '{Settings.CustomDownloadFolder}' is stored for installer downloads.");
        }

        if (RestartRequired)
        {
            RestartStatusText = "Restart recommended: some apps or drivers may not work correctly until the PC is restarted.";
            _loggingService.LogWarning(
                $"Restart recommended after install. RestartItems={restartCount}, Installed={successCount}, Failed={failedCount}, Cancelled={cancelledCount}, Skipped={skippedCount}");
        }
        else
        {
            RestartStatusText = "No restart required.";
            _restartDecisionFinalized = true;
            _loggingService.LogInfo("Installation finished with no restart required.");
        }

    }

    private async Task ApplyPostInstallRestartBehaviorAsync()
    {
        if (!RestartRequired)
        {
            return;
        }

        switch (Settings.RestartBehavior)
        {
            case AppSettings.RestartAutomatically:
                _loggingService.LogInfo("Restart behavior is set to RestartAutomatically. Triggering restart now.");
                var restarted = await TryRestartSystemAsync();
                if (restarted)
                {
                    _restartDecisionFinalized = true;
                    RestartStatusText = "Automatic restart command sent. System should restart shortly.";
                    InstallStatusText = "Automatic restart command sent.";
                }
                else
                {
                    RestartStatusText = "Automatic restart failed. Please restart manually.";
                }

                OnPropertyChanged(nameof(ShowRestartActions));
                OnPropertyChanged(nameof(ShowPrimaryRestartActions));
                break;

            case AppSettings.RestartNever:
                _restartDecisionFinalized = true;
                RestartStatusText = "Restart was required, but automatic restart is disabled. Please restart manually later.";
                _loggingService.LogInfo("Restart behavior is set to NeverRestart. User must restart manually.");
                OnPropertyChanged(nameof(ShowRestartActions));
                OnPropertyChanged(nameof(ShowPrimaryRestartActions));
                break;

            default:
                RestartStatusText = "Restart recommended: some apps or drivers may not work correctly until the PC is restarted.";
                break;
        }
    }

    private void ApplyInstallResultToApp(InstallResult result)
    {
        var app = _apps.FirstOrDefault(candidate => candidate.Id.Equals(result.AppId, StringComparison.OrdinalIgnoreCase));
        if (app is null)
        {
            return;
        }

        if (result.Success)
        {
            app.IsInstalled = true;
            app.InstalledVersion = string.IsNullOrWhiteSpace(app.Version) ? app.InstalledVersion : app.Version;
            app.HasInstallFailed = false;
            app.StatusBadge = app.HasUpdateAvailable && !app.UserDisabledScanning
                ? AppItem.StatusUpdateAvailable
                : AppItem.StatusInstalled;
        }
        else if (result.Cancelled)
        {
            app.HasInstallFailed = false;
            app.StatusBadge = AppItem.StatusCancelled;
        }
        else if (result.Skipped)
        {
            if (result.Message.Contains("Already installed on this PC", StringComparison.OrdinalIgnoreCase))
            {
                app.IsInstalled = true;
                app.HasInstallFailed = false;
                app.StatusBadge = app.HasUpdateAvailable && !app.UserDisabledScanning
                    ? AppItem.StatusUpdateAvailable
                    : AppItem.StatusInstalled;
            }
            else if (result.Message.Contains("requires manual installer interaction", StringComparison.OrdinalIgnoreCase) ||
                     result.Message.Contains("requires manual install", StringComparison.OrdinalIgnoreCase))
            {
                app.HasInstallFailed = false;
                app.StatusBadge = AppItem.StatusNeedsManualInstall;
            }
            else
            {
                app.StatusBadge = app.WillBeSkipped ? AppItem.StatusWillBeSkipped : AppItem.StatusSkipped;
            }
        }
        else
        {
            app.HasInstallFailed = true;
            app.StatusBadge = AppItem.StatusFailed;
        }

        if (result.RequiresRestart)
        {
            app.RequiresRestartHint = true;
        }

        if (ReferenceEquals(SelectedDetailApp, app))
        {
            NotifyAppDetailsStateChanged();
        }
    }

    private async Task RefreshInstalledStatesAfterInstallAsync()
    {
        var installedApps = await Task.Run(() =>
            _detectionService.DetectInstalledAppStates(_apps.ToList(), _currentPlatformId));

        ApplyInstalledAppResults(installedApps);
        NotifyAppSummaryStateChanged();
        await RefreshAvailableAppUpdatesAsync(rerunInstalledDetection: false, updateStatusText: false);
    }

    private void ReplaceCatalogApps(
        IReadOnlyList<AppItem> refreshedApps,
        IReadOnlySet<string> selectedIds,
        string? detailAppId,
        bool wasDetailsOpen)
    {
        _suppressAppSelectionHandling = true;
        try
        {
            foreach (var existingApp in _apps)
            {
                existingApp.PropertyChanged -= HandleAppPropertyChanged;
            }

            _apps.Clear();
            foreach (var app in refreshedApps)
            {
                app.IsSelected = selectedIds.Contains(app.Id);
                ApplyPlatformFlags(app);
                app.PropertyChanged += HandleAppPropertyChanged;
                _apps.Add(app);
            }
        }
        finally
        {
            _suppressAppSelectionHandling = false;
        }

        if (!wasDetailsOpen)
        {
            return;
        }

        var refreshedDetailApp = _apps.FirstOrDefault(app =>
            app.Id.Equals(detailAppId, StringComparison.OrdinalIgnoreCase));

        if (refreshedDetailApp is null)
        {
            CloseAppDetails(logAction: false);
            return;
        }

        SelectedDetailApp = refreshedDetailApp;
        IsAppDetailsOpen = true;
    }

    private void ApplyPlatformFlags(AppItem app)
    {
        app.IsSupportedOnCurrentPlatform = _platformService.IsSupportedOnPlatform(app.SupportedPlatforms, _currentPlatformId);
        app.WillBeSkipped = app.IsSelected && !app.IsSupportedOnCurrentPlatform;
        app.RowOpacity = app.IsSupportedOnCurrentPlatform ? 1.0 : 0.78;

        if (app.WillBeSkipped)
        {
            app.StatusBadge = AppItem.StatusWillBeSkipped;
            if (app.IsRecommended)
            {
                var unsupportedMessage = $"Unsupported on {CurrentPlatform}; it will be skipped.";
                if (string.IsNullOrWhiteSpace(app.RecommendationReason))
                {
                    app.RecommendationReason = unsupportedMessage;
                }
                else if (!app.RecommendationReason.Contains(unsupportedMessage, StringComparison.OrdinalIgnoreCase))
                {
                    app.RecommendationReason = $"{app.RecommendationReason} {unsupportedMessage}";
                }
            }
            else
            {
                app.RecommendationReason = UnsupportedSelectionNote;
            }

            return;
        }

        if (!app.IsSupportedOnCurrentPlatform)
        {
            app.StatusBadge = AppItem.StatusUnsupportedOnCurrentOs;
            if (app.IsRecommended)
            {
                if (string.IsNullOrWhiteSpace(app.RecommendationReason))
                {
                    app.RecommendationReason = $"Recommended by detected hardware, but unsupported on {CurrentPlatform}.";
                }
            }
            else if (app.RecommendationReason == UnsupportedSelectionNote)
            {
                app.RecommendationReason = string.Empty;
            }

            return;
        }

        if (app.IsInstalled && !app.HasInstallFailed)
        {
            app.StatusBadge = app.HasUpdateAvailable && !app.UserDisabledScanning
                ? AppItem.StatusUpdateAvailable
                : AppItem.StatusInstalled;
        }
        else if (app.IsSelected &&
                 Settings.SilentInstall &&
                 !app.UserDisabledSilentInstall &&
                 !app.SupportsSilentInstall &&
                 !app.HasInstallFailed)
        {
            app.StatusBadge = AppItem.StatusNeedsManualInstall;
        }
        else if (!app.HasInstallFailed)
        {
            app.StatusBadge = app.IsSelected ? AppItem.StatusSelected : AppItem.StatusAvailable;
        }

        if (app.RecommendationReason == UnsupportedSelectionNote)
        {
            app.RecommendationReason = string.Empty;
        }

        if (ReferenceEquals(SelectedDetailApp, app))
        {
            NotifyAppDetailsStateChanged();
        }
    }

    private bool CanShowRestartActions()
    {
        return !IsInstalling && ShowRestartActions;
    }

    private bool CanConfirmRestartNow()
    {
        return !IsInstalling && RestartRequired && IsRestartConfirmationVisible;
    }

    private void RequestRestartNow()
    {
        if (!CanShowRestartActions())
        {
            return;
        }

        IsRestartConfirmationVisible = true;
        RestartStatusText = "Confirm restart now? Unsaved work in other applications may be lost.";
        _loggingService.LogInfo("User selected Restart Now. Waiting for confirmation.");
    }

    private void CancelRestartNow()
    {
        IsRestartConfirmationVisible = false;
        RestartStatusText = "Restart is still recommended. You can restart now or later.";
        _loggingService.LogInfo("User cancelled restart confirmation dialog state.");
    }

    private void RestartLater()
    {
        if (!CanShowRestartActions())
        {
            return;
        }

        _restartDecisionFinalized = true;
        IsRestartConfirmationVisible = false;
        RestartStatusText = "Restart postponed. Installation is complete; restart later when convenient.";
        InstallStatusText = "Installation finished. Restart postponed.";
        _loggingService.LogInfo("User chose Skip / Restart Later.");
        OnPropertyChanged(nameof(ShowRestartActions));
        OnPropertyChanged(nameof(ShowPrimaryRestartActions));
        UpdateCommandStates();
    }

    private async Task ConfirmRestartNowAsync()
    {
        if (!CanConfirmRestartNow())
        {
            return;
        }

        _loggingService.LogInfo("User confirmed Restart Now.");
        var restarted = await TryRestartSystemAsync();
        if (restarted)
        {
            _restartDecisionFinalized = true;
            IsRestartConfirmationVisible = false;
            RestartStatusText = "Restart command sent. System should restart shortly.";
            InstallStatusText = "Restart command sent.";
            _loggingService.LogInfo("System restart command executed successfully.");
        }
        else
        {
            IsRestartConfirmationVisible = false;
            RestartStatusText = "Could not trigger automatic restart. Please restart manually.";
            _loggingService.LogError("Failed to execute system restart command.");
        }

        OnPropertyChanged(nameof(ShowRestartActions));
        OnPropertyChanged(nameof(ShowPrimaryRestartActions));
        UpdateCommandStates();
    }

    private async Task<bool> TryRestartSystemAsync()
    {
        try
        {
            ProcessStartInfo? startInfo = _currentPlatformId switch
            {
                PlatformService.Windows => new ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = "/r /t 0",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                PlatformService.Linux => new ProcessStartInfo
                {
                    FileName = "sh",
                    Arguments = "-c \"systemctl reboot || shutdown -r now\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                _ => null
            };

            if (startInfo is null)
            {
                _loggingService.LogWarning("Restart command is not available for unknown platform.");
                return false;
            }

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Restart command failed: {ex.Message}");
            return false;
        }
    }

    private static string FormatScheduledTimestamp(DateTime value)
    {
        if (value == DateTime.MinValue)
        {
            return "Never";
        }

        var localValue = value.Kind switch
        {
            DateTimeKind.Utc => value.ToLocalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime(),
            _ => value
        };

        return localValue.ToString("MMM d, yyyy h:mm tt");
    }

    private static AppItem CloneAppForBackgroundWork(AppItem source)
    {
        return new AppItem
        {
            Id = source.Id,
            Name = source.Name,
            Category = source.Category,
            PublisherName = source.PublisherName,
            HomepageUrl = source.HomepageUrl,
            Description = source.Description,
            IconPath = source.IconPath,
            WingetId = source.WingetId,
            Version = source.Version,
            InstalledVersion = source.InstalledVersion,
            License = source.License,
            ReleaseNotesUrl = source.ReleaseNotesUrl,
            Tags = source.Tags.ToList(),
            Dependencies = source.Dependencies.ToList(),
            IsPortable = source.IsPortable,
            PortableInstallPath = source.PortableInstallPath,
            UserDisabledSilentInstall = source.UserDisabledSilentInstall,
            UserDisabledScanning = source.UserDisabledScanning,
            UserDisabledAutoUpdate = source.UserDisabledAutoUpdate,
            UserTrustedInstallScripts = source.UserTrustedInstallScripts,
            SupportedPlatforms = new PlatformSupport
            {
                Windows = source.SupportedPlatforms.Windows,
                Linux = source.SupportedPlatforms.Linux
            },
            WindowsInstall = CloneInstallDefinition(source.WindowsInstall),
            LinuxInstall = CloneInstallDefinition(source.LinuxInstall),
            IsSupportedOnCurrentPlatform = source.IsSupportedOnCurrentPlatform,
            SupportsSilentInstall = source.SupportsSilentInstall,
            IsInstalled = source.IsInstalled,
            IsHidden = source.IsHidden
        };
    }

    private static InstallDefinition? CloneInstallDefinition(InstallDefinition? source)
    {
        if (source is null)
        {
            return null;
        }

        return new InstallDefinition
        {
            InstallerUrl = source.InstallerUrl,
            InstallerUrl32 = source.InstallerUrl32,
            InstallerUrl64 = source.InstallerUrl64,
            InstallerUrlArm64 = source.InstallerUrlArm64,
            InstallerFileName = source.InstallerFileName,
            Sha256 = source.Sha256,
            Sha25632 = source.Sha25632,
            Sha25664 = source.Sha25664,
            Command = source.Command,
            SilentCommand = source.SilentCommand,
            Arguments = source.Arguments,
            SilentArguments = source.SilentArguments,
            SilentArgumentsArm64 = source.SilentArgumentsArm64,
            Architecture = source.Architecture,
            HasArm64Support = source.HasArm64Support,
            PortableArchiveUrl = source.PortableArchiveUrl,
            PortableExecutable = source.PortableExecutable,
            PortableArchiveType = source.PortableArchiveType,
            PortableSubfolder = source.PortableSubfolder,
            VirusTotalUrl = source.VirusTotalUrl,
            VirusTotalRatio = source.VirusTotalRatio,
            VirusTotalScanDate = source.VirusTotalScanDate,
            PreInstallScript = source.PreInstallScript,
            PostInstallScript = source.PostInstallScript,
            RequiresRestart = source.RequiresRestart,
            RequiresElevation = source.RequiresElevation,
            NeedsManualInstall = source.NeedsManualInstall,
            VerificationTimeoutSeconds = source.VerificationTimeoutSeconds,
            DetectDisplayNameContains = source.DetectDisplayNameContains,
            DetectExecutable = source.DetectExecutable
        };
    }

    private static string QuoteArgument(string value)
    {
        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static string ResolveWingetExecutablePathForCommands()
    {
        var localAlias = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "WindowsApps",
            "winget.exe");

        return string.IsNullOrWhiteSpace(localAlias) ? "winget.exe" : localAlias;
    }

    private void UpdateCommandStates()
    {
        _installCommand.RaiseCanExecuteChanged();
        _refreshCatalogCommand.RaiseCanExecuteChanged();
        _saveProfileCommand.RaiseCanExecuteChanged();
        _confirmSaveProfileCommand.RaiseCanExecuteChanged();
        _cancelSaveProfileCommand.RaiseCanExecuteChanged();
        _loadProfileCommand.RaiseCanExecuteChanged();
        _exportProfileCommand.RaiseCanExecuteChanged();
        _loadSavedProfileCommand.RaiseCanExecuteChanged();
        _deleteSavedProfileCommand.RaiseCanExecuteChanged();
        _showRecommendedFilterCommand.RaiseCanExecuteChanged();
        _showUpdatesFilterCommand.RaiseCanExecuteChanged();
        _dismissUpdateCommand.RaiseCanExecuteChanged();
        _downloadUpdateCommand.RaiseCanExecuteChanged();
        _manualCheckForUpdatesCommand.RaiseCanExecuteChanged();
        _runScheduledUpdatesNowCommand.RaiseCanExecuteChanged();
        _clearLogsCommand.RaiseCanExecuteChanged();
        _copyLogsCommand.RaiseCanExecuteChanged();
        _copySelectedLogCommand.RaiseCanExecuteChanged();
        _exportLogsCommand.RaiseCanExecuteChanged();
        _browsePortableFolderCommand.RaiseCanExecuteChanged();
        _installAppCommand.RaiseCanExecuteChanged();
        _updateAppCommand.RaiseCanExecuteChanged();
        _uninstallCommand.RaiseCanExecuteChanged();
        _openInstallLocationCommand.RaiseCanExecuteChanged();
        _copyToClipboardCommand.RaiseCanExecuteChanged();
        _toggleSilentInstallPreferenceCommand.RaiseCanExecuteChanged();
        _toggleScanningPreferenceCommand.RaiseCanExecuteChanged();
        _pauseInstallCommand.RaiseCanExecuteChanged();
        _clearQueueCommand.RaiseCanExecuteChanged();
        _navigateDashboardCommand.RaiseCanExecuteChanged();
        _navigateAppsCommand.RaiseCanExecuteChanged();
        _navigateDriversCommand.RaiseCanExecuteChanged();
        _navigateMyListsCommand.RaiseCanExecuteChanged();
        _navigateUpdatesCommand.RaiseCanExecuteChanged();
        _navigateHistoryCommand.RaiseCanExecuteChanged();
        _navigateLogsCommand.RaiseCanExecuteChanged();
        _navigateAboutCommand.RaiseCanExecuteChanged();
        _openAccountProfileCommand.RaiseCanExecuteChanged();
        _openAccountSettingsCommand.RaiseCanExecuteChanged();
        _openAboutGitHubCommand.RaiseCanExecuteChanged();
        _requestRestartNowCommand.RaiseCanExecuteChanged();
        _confirmRestartNowCommand.RaiseCanExecuteChanged();
        _cancelRestartCommand.RaiseCanExecuteChanged();
        _restartLaterCommand.RaiseCanExecuteChanged();
        _resetSettingsCommand.RaiseCanExecuteChanged();
    }

    private void NotifyAppSummaryStateChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedFooterText));
        OnPropertyChanged(nameof(SelectedAppsCount));
        OnPropertyChanged(nameof(SelectedAppsSizeMB));
        OnPropertyChanged(nameof(SelectedAppsTimeMins));
        OnPropertyChanged(nameof(RecommendedCount));
        OnPropertyChanged(nameof(HasRecommendedApps));
        OnPropertyChanged(nameof(UpdateAvailableCount));
        OnPropertyChanged(nameof(HasUpdatesAvailable));
        OnPropertyChanged(nameof(RecommendedAppsSummary));
        OnPropertyChanged(nameof(UnsupportedSelectedCount));
        OnPropertyChanged(nameof(HasUnsupportedSelectedApps));
        _showRecommendedFilterCommand.RaiseCanExecuteChanged();
        _showUpdatesFilterCommand.RaiseCanExecuteChanged();
    }

    private void NotifyAppDetailsStateChanged()
    {
        OnPropertyChanged(nameof(SelectedDetailApp));
        OnPropertyChanged(nameof(SelectedDetailDescription));
        OnPropertyChanged(nameof(SelectedDetailPlatformsText));
        OnPropertyChanged(nameof(SelectedDetailInstallSupportText));
        OnPropertyChanged(nameof(SelectedDetailSupportStatusText));
        OnPropertyChanged(nameof(SelectedDetailCatalogVersionText));
        OnPropertyChanged(nameof(SelectedDetailInstalledVersionText));
        OnPropertyChanged(nameof(SelectedDetailUpdateStatusText));
        OnPropertyChanged(nameof(HasSelectedDetailRecommendation));
    }

    private static bool IsUnsupportedSkippedResult(InstallResult result)
    {
        return result.Skipped &&
               result.Message.Contains("unsupported", StringComparison.OrdinalIgnoreCase);
    }

    public sealed record SettingChoice(string Value, string Label);
}


