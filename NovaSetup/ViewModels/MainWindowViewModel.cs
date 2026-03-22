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

    private readonly ObservableCollection<AppItem> _apps = new();
    private readonly ObservableCollection<AppItem> _visibleApps = new();
    private readonly ObservableCollection<NovaProfile> _savedProfiles = new();
    private readonly ObservableCollection<LogEntry> _filteredLiveLogs = new();
    private readonly ObservableCollection<InstallResult> _installResults = new();
    private readonly ObservableCollection<InstallResult> _installedResults = new();
    private readonly ObservableCollection<InstallResult> _failedResults = new();
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
    private readonly AsyncRelayCommand _clearLogsCommand;
    private readonly AsyncRelayCommand _copyLogsCommand;
    private readonly AsyncRelayCommand _copySelectedLogCommand;
    private readonly AsyncRelayCommand _exportLogsCommand;
    private readonly RelayCommand _openPublisherCommand;
    private readonly RelayCommand _openAboutGitHubCommand;
    private readonly RelayCommand _showAppDetailsCommand;
    private readonly RelayCommand _closeAppDetailsCommand;
    private readonly RelayCommand _pauseInstallCommand;
    private readonly RelayCommand _exportReportCommand;
    private readonly RelayCommand _toggleSettingsPanelCommand;
    private readonly RelayCommand _toggleAccountMenuCommand;
    private readonly RelayCommand _openAccountProfileCommand;
    private readonly RelayCommand _openAccountSettingsCommand;
    private readonly RelayCommand _navigateDashboardCommand;
    private readonly RelayCommand _navigateAppsCommand;
    private readonly RelayCommand _navigateDriversCommand;
    private readonly RelayCommand _navigateMyListsCommand;
    private readonly RelayCommand _navigateHistoryCommand;
    private readonly RelayCommand _navigateLogsCommand;
    private readonly RelayCommand _navigateAboutCommand;
    private readonly RelayCommand _requestRestartNowCommand;
    private readonly AsyncRelayCommand _confirmRestartNowCommand;
    private readonly RelayCommand _cancelRestartCommand;
    private readonly RelayCommand _restartLaterCommand;
    private readonly AsyncRelayCommand _resetSettingsCommand;

    private bool _isInitialized;
    private bool _isInitializing;
    private bool _isInstalling;
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
    private bool _isUpdatesFilter;
    private string _selectedLogLevelFilter = LogFilterAll;
    private double _progressValue;
    private CancellationTokenSource? _settingsSaveCts;
    private CancellationTokenSource? _startupDetectionCts;

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

    public MainWindowViewModel(
        PlatformService platformService,
        CatalogService catalogService,
        SelectionService selectionService,
        DetectionService detectionService,
        InstallerService installerService,
        LoggingService loggingService,
        BrowserService browserService,
        SettingsService settingsService,
        ProfileService profileService)
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

        Settings = new AppSettings();
        Settings.PropertyChanged += HandleSettingsChanged;
        LoggingService.DeveloperModeAccessor = () => Settings.DeveloperMode;
        _selectionService.SettingsAccessor = () => Settings;

        _installCommand = new AsyncRelayCommand(InstallSelectedAsync, CanInstall);
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
        _clearLogsCommand = new AsyncRelayCommand(ClearLogsAsync, () => IsDeveloperModeEnabled);
        _copyLogsCommand = new AsyncRelayCommand(CopyLogsAsync, () => IsDeveloperModeEnabled && FilteredLiveLogs.Count > 0);
        _copySelectedLogCommand = new AsyncRelayCommand(CopySelectedLogAsync, () => IsDeveloperModeEnabled && SelectedLiveLogEntry is not null);
        _exportLogsCommand = new AsyncRelayCommand(ExportLogsAsync, () => IsDeveloperModeEnabled && FilteredLiveLogs.Count > 0);
        _openPublisherCommand = new RelayCommand(OpenPublisherHomepage);
        _openAboutGitHubCommand = new RelayCommand(_ => OpenAboutGitHub(), _ => !IsInstalling);
        _showAppDetailsCommand = new RelayCommand(ShowAppDetails);
        _closeAppDetailsCommand = new RelayCommand(_ => CloseAppDetails());
        _pauseInstallCommand = new RelayCommand(_ => PauseInstallPlaceholder(), _ => IsInstalling);
        _exportReportCommand = new RelayCommand(_ => ExportReportPlaceholder());
        _toggleSettingsPanelCommand = new RelayCommand(_ => ToggleSettingsPanel());
        _toggleAccountMenuCommand = new RelayCommand(_ => ToggleAccountMenu());
        _openAccountProfileCommand = new RelayCommand(_ => OpenAccountProfile(), _ => !IsInstalling);
        _openAccountSettingsCommand = new RelayCommand(_ => OpenAccountSettings());
        _navigateDashboardCommand = new RelayCommand(_ => NavigateTo(SectionDashboard), _ => !IsInstalling);
        _navigateAppsCommand = new RelayCommand(_ => NavigateTo(SectionApps), _ => !IsInstalling);
        _navigateDriversCommand = new RelayCommand(_ => NavigateToDriversFilter(), _ => !IsInstalling);
        _navigateMyListsCommand = new RelayCommand(_ => NavigateTo(SectionMyLists), _ => !IsInstalling);
        _navigateHistoryCommand = new RelayCommand(_ => NavigateTo(SectionHistory), _ => !IsInstalling);
        _navigateLogsCommand = new RelayCommand(_ => NavigateTo(SectionLogs), _ => !IsInstalling);
        _navigateAboutCommand = new RelayCommand(_ => NavigateTo(SectionAbout), _ => !IsInstalling);
        _requestRestartNowCommand = new RelayCommand(_ => RequestRestartNow(), _ => CanShowRestartActions());
        _confirmRestartNowCommand = new AsyncRelayCommand(ConfirmRestartNowAsync, CanConfirmRestartNow);
        _cancelRestartCommand = new RelayCommand(_ => CancelRestartNow(), _ => IsRestartConfirmationVisible);
        _restartLaterCommand = new RelayCommand(_ => RestartLater(), _ => CanShowRestartActions());
        _resetSettingsCommand = new AsyncRelayCommand(ResetSettingsAsync, () => !IsInstalling);

        HookCollectionNotifications();
        RefreshFilteredLiveLogs();
    }

    public AppSettings Settings { get; }

    public string CurrentPlatform
    {
        get => _currentPlatform;
        private set => SetProperty(ref _currentPlatform, value);
    }

    public ObservableCollection<AppItem> Apps => _apps;

    public ObservableCollection<AppItem> VisibleApps => _visibleApps;

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

    public string SelectedFooterText => $"Visible apps: {VisibleAppCount} • Selected apps: {SelectedCount} • Download size: {SelectedCount * 30} MB";

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
        _ => "Choose what to install"
    };

    public string HomeSubtitle => _currentSection switch
    {
        SectionDashboard => "Review apps, drivers, and setup choices from one place.",
        SectionDrivers => "Focused view for drivers, GPU tools, and accessory software.",
        SectionMyLists => "Only selected apps are shown here so you can review your list quickly.",
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

    public bool IsHistorySelected => string.Equals(_currentSection, SectionHistory, StringComparison.Ordinal);
    public bool IsHistoryUnselected => !IsHistorySelected;

    public bool IsLogsSelected => string.Equals(_currentSection, SectionLogs, StringComparison.Ordinal);
    public bool IsLogsUnselected => !IsLogsSelected;

    public bool IsAboutSelected => string.Equals(_currentSection, SectionAbout, StringComparison.Ordinal);
    public bool IsAboutUnselected => !IsAboutSelected;

    public bool IsHomeScreenVisible => !IsInstalling && !IsHistorySelected && !IsLogsSelected && !IsAboutSelected;

    public bool IsInstallScreenVisible => IsInstalling;

    public bool IsSummaryScreenVisible => !IsInstalling && IsHistorySelected && HasInstallResults;

    public bool IsHistoryEmptyScreenVisible => !IsInstalling && IsHistorySelected && !HasInstallResults;

    public bool IsLogsScreenVisible => !IsInstalling && IsLogsSelected;

    public bool IsAboutScreenVisible => !IsInstalling && IsAboutSelected;

    public bool IsSelectionPhase => !IsHistorySelected && !IsLogsSelected && !IsAboutSelected;

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

    public string InstallStatusText
    {
        get => _installStatusText;
        private set => SetProperty(ref _installStatusText, value);
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

    public ICommand RefreshCatalogCommand => _refreshCatalogCommand;

    public ICommand ShowUpdatesFilterCommand => _showUpdatesFilterCommand;

    public ICommand ShowRecommendedFilterCommand => _showRecommendedFilterCommand;

    public ICommand DismissUpdateCommand => _dismissUpdateCommand;

    public ICommand DownloadUpdateCommand => _downloadUpdateCommand;

    public ICommand ManualCheckForUpdatesCommand => _manualCheckForUpdatesCommand;

    public ICommand ClearLogsCommand => _clearLogsCommand;

    public ICommand CopyLogsCommand => _copyLogsCommand;

    public ICommand CopySelectedLogCommand => _copySelectedLogCommand;

    public ICommand ExportLogsCommand => _exportLogsCommand;

    public ICommand OpenPublisherCommand => _openPublisherCommand;

    public ICommand OpenAboutGitHubCommand => _openAboutGitHubCommand;

    public ICommand ShowAppDetailsCommand => _showAppDetailsCommand;

    public ICommand CloseAppDetailsCommand => _closeAppDetailsCommand;

    public ICommand PauseInstallCommand => _pauseInstallCommand;

    public ICommand ExportReportCommand => _exportReportCommand;

    public ICommand ToggleSettingsPanelCommand => _toggleSettingsPanelCommand;

    public ICommand ToggleAccountMenuCommand => _toggleAccountMenuCommand;

    public ICommand OpenAccountProfileCommand => _openAccountProfileCommand;

    public ICommand OpenAccountSettingsCommand => _openAccountSettingsCommand;

    public ICommand NavigateDashboardCommand => _navigateDashboardCommand;

    public ICommand NavigateAppsCommand => _navigateAppsCommand;

    public ICommand NavigateDriversCommand => _navigateDriversCommand;

    public ICommand NavigateMyListsCommand => _navigateMyListsCommand;

    public ICommand NavigateHistoryCommand => _navigateHistoryCommand;

    public ICommand NavigateLogsCommand => _navigateLogsCommand;

    public ICommand NavigateAboutCommand => _navigateAboutCommand;

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
        };
        _savedProfiles.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasSavedProfiles));
            OnPropertyChanged(nameof(HasNoSavedProfiles));
            _loadSavedProfileCommand.RaiseCanExecuteChanged();
            _deleteSavedProfileCommand.RaiseCanExecuteChanged();
        };
        LoggingService.LiveLogs.CollectionChanged += HandleLiveLogsCollectionChanged;
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

        LogSettingChanged(e.PropertyName);
        ScheduleSettingsSave();

        if (Settings.SaveProfilesAutomatically)
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
        OnPropertyChanged(nameof(IsCustomDownloadFolderVisible));
        OnPropertyChanged(nameof(IsDeveloperModeEnabled));
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
        if (_apps.Count == 0 && _currentPlatformId == PlatformService.Unknown)
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
            return;
        }

        _currentSection = section;
        IsSettingsPanelOpen = false;
        IsAccountMenuOpen = false;
        CloseAppDetails(logAction: false);

        _loggingService.LogInfo($"Navigation changed to '{section}'.");

        if (section == SectionHistory && !HasInstallResults)
        {
            StatusText = "No install history yet.";
        }
        else if (section == SectionLogs)
        {
            StatusText = "Logs page selected. Use Open Log File to inspect the current log.";
        }
        else if (section == SectionAbout)
        {
            StatusText = $"About page selected. Running {AboutVersionText}.";
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
        if (parameter is not AppItem app)
        {
            return;
        }

        if (!_browserService.OpenPublisherHomepage(app.HomepageUrl))
        {
            StatusText = $"Could not open homepage for {app.Name}.";
        }
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
        StatusText = "Pause is not available yet. Installation continues.";
        _loggingService.LogInfo("Pause requested (placeholder).");
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

        StatusText = $"{BuildLoadedStatusText(detectionPending: false)} Refreshed current catalog and app state.";
        _loggingService.LogInfo("Manual refresh completed.");
        _loggingService.LogInfo(StatusText);
    }

    private void HandleAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressAppSelectionHandling || sender is not AppItem app || e.PropertyName != nameof(AppItem.IsSelected))
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

        ResetInstallOutputState();
        IsSettingsPanelOpen = false;
        CloseAppDetails(logAction: false);
        IsInstalling = true;

        InstallStatusText = $"Starting installation for {selectedApps.Count} app(s)...";
        _loggingService.LogInfo(InstallStatusText);
        _loggingService.LogInfo(
            $"Install settings: Silent={Settings.SilentInstall}, SelfDelete={Settings.SelfDeleteAfterInstall}, Parallel={Settings.ParallelInstall}, OsSupportedApps={Settings.OsSupportedApps}, DownloadLocationMode={Settings.DownloadLocationMode}, CustomDownloadFolder='{Settings.CustomDownloadFolder}', KeepInstallers={Settings.KeepInstallersAfterInstall}, RestartBehavior={Settings.RestartBehavior}");

        if (Settings.ParallelInstall && selectedApps.Count > 1)
        {
            _loggingService.LogInfo("Parallel install is enabled in settings, but the installer currently uses the stable sequential pipeline.");
        }

        var processedCount = 0;
        foreach (var app in selectedApps)
        {
            InstallStatusText = $"Installing {app.Name} ({processedCount + 1}/{selectedApps.Count})...";
            var batchResults = await _installerService.InstallSelectedAppsAsync(
                new[] { app },
                _currentPlatformId,
                silentInstallEnabled: Settings.SilentInstall,
                keepInstallersAfterInstall: Settings.KeepInstallersAfterInstall,
                downloadLocationMode: Settings.DownloadLocationMode,
                customDownloadFolder: Settings.CustomDownloadFolder);

            foreach (var result in batchResults)
            {
                AddInstallResult(result);
                ApplyInstallResultToApp(result);
            }

            processedCount++;
            ProgressValue = Math.Round((double)processedCount / selectedApps.Count * 100.0, 1);
        }

        await RefreshInstalledStatesAfterInstallAsync();
        FinalizeInstallSummary();
        IsInstalling = false;
        await ApplyPostInstallRestartBehaviorAsync();
        UpdateCommandStates();
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
        _skippedResults.Clear();
        _restartRequiredResults.Clear();
        _unsupportedSkippedResults.Clear();
    }

    private void AddInstallResult(InstallResult result)
    {
        _installResults.Add(result);

        if (result.Success)
        {
            _installedResults.Add(result);
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
        var restartCount = _restartRequiredResults.Count;

        RestartRequired = restartCount > 0;
        ProgressValue = 100;
        InstallStatusText = "Installation finished.";
        InstallSummaryText = $"Installed: {successCount} | Failed: {failedCount} | Skipped: {skippedCount} | Restart: {restartCount}";
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
                $"Restart recommended after install. RestartItems={restartCount}, Installed={successCount}, Failed={failedCount}, Skipped={skippedCount}");
        }
        else
        {
            RestartStatusText = "No restart required.";
            _restartDecisionFinalized = true;
            _loggingService.LogInfo("Installation finished with no restart required.");
        }

        NavigateTo(SectionHistory);
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
            app.StatusBadge = app.HasUpdateAvailable ? "Update Available" : "Installed";
        }
        else if (result.Skipped)
        {
            if (result.Message.Contains("Already installed on this PC", StringComparison.OrdinalIgnoreCase))
            {
                app.IsInstalled = true;
                app.HasInstallFailed = false;
                app.StatusBadge = app.HasUpdateAvailable ? "Update Available" : "Installed";
            }
            else if (result.Message.Contains("requires manual installer interaction", StringComparison.OrdinalIgnoreCase) ||
                     result.Message.Contains("requires manual install", StringComparison.OrdinalIgnoreCase))
            {
                app.HasInstallFailed = false;
                app.StatusBadge = "Needs Manual Install";
            }
            else
            {
                app.StatusBadge = app.WillBeSkipped ? "Will Be Skipped" : "Skipped";
            }
        }
        else
        {
            app.HasInstallFailed = true;
            app.StatusBadge = "Failed";
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
            app.StatusBadge = "Will Be Skipped";
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
            app.StatusBadge = "Unsupported on this OS";
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
            app.StatusBadge = app.HasUpdateAvailable ? "Update Available" : "Installed";
        }
        else if (app.IsSelected && Settings.SilentInstall && !app.SupportsSilentInstall && !app.HasInstallFailed)
        {
            app.StatusBadge = "Needs Manual Install";
        }
        else if (!app.HasInstallFailed)
        {
            app.StatusBadge = app.IsSelected ? "Selected" : "Available";
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
        _clearLogsCommand.RaiseCanExecuteChanged();
        _copyLogsCommand.RaiseCanExecuteChanged();
        _copySelectedLogCommand.RaiseCanExecuteChanged();
        _exportLogsCommand.RaiseCanExecuteChanged();
        _pauseInstallCommand.RaiseCanExecuteChanged();
        _navigateDashboardCommand.RaiseCanExecuteChanged();
        _navigateAppsCommand.RaiseCanExecuteChanged();
        _navigateDriversCommand.RaiseCanExecuteChanged();
        _navigateMyListsCommand.RaiseCanExecuteChanged();
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


