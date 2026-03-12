using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia;
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

    private readonly PlatformService _platformService;
    private readonly CatalogService _catalogService;
    private readonly SelectionService _selectionService;
    private readonly DetectionService _detectionService;
    private readonly InstallerService _installerService;
    private readonly LoggingService _loggingService;
    private readonly BrowserService _browserService;
    private readonly SettingsService _settingsService;

    private readonly ObservableCollection<AppItem> _apps = new();
    private readonly ObservableCollection<AppItem> _visibleApps = new();
    private readonly ObservableCollection<InstallResult> _installResults = new();
    private readonly ObservableCollection<InstallResult> _installedResults = new();
    private readonly ObservableCollection<InstallResult> _failedResults = new();
    private readonly ObservableCollection<InstallResult> _skippedResults = new();
    private readonly ObservableCollection<InstallResult> _restartRequiredResults = new();
    private readonly ObservableCollection<InstallResult> _unsupportedSkippedResults = new();

    private readonly AsyncRelayCommand _installCommand;
    private readonly RelayCommand _saveListCommand;
    private readonly RelayCommand _openLogFileCommand;
    private readonly RelayCommand _openPublisherCommand;
    private readonly RelayCommand _showAppDetailsCommand;
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
    private readonly RelayCommand _requestRestartNowCommand;
    private readonly AsyncRelayCommand _confirmRestartNowCommand;
    private readonly RelayCommand _cancelRestartCommand;
    private readonly RelayCommand _restartLaterCommand;
    private readonly AsyncRelayCommand _resetSettingsCommand;

    private bool _isInitialized;
    private bool _isInstalling;
    private bool _restartRequired;
    private bool _isRestartConfirmationVisible;
    private bool _restartDecisionFinalized;
    private bool _isSettingsPanelOpen;
    private bool _isAccountMenuOpen;
    private bool _suppressSettingsLogging;
    private bool _installedAppsDetected;
    private bool _updatingFilterFlags;
    private string _currentPlatformId = PlatformService.Unknown;
    private string _currentPlatform = "Unknown OS";
    private string _currentProfileName = "Website Starter";
    private string _statusText = "Ready.";
    private string _installStatusText = "Install has not started.";
    private string _installSummaryText = string.Empty;
    private string _restartStatusText = string.Empty;
    private string _currentSection = SectionApps;
    private string _searchText = string.Empty;
    private string _selectedFilter = "All";
    private bool _isAllFilter = true;
    private bool _isGamesFilter;
    private bool _isDevToolsFilter;
    private bool _isUtilitiesFilter;
    private double _progressValue;
    private CancellationTokenSource? _settingsSaveCts;

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

    public MainWindowViewModel(
        PlatformService platformService,
        CatalogService catalogService,
        SelectionService selectionService,
        DetectionService detectionService,
        InstallerService installerService,
        LoggingService loggingService,
        BrowserService browserService,
        SettingsService settingsService)
    {
        _platformService = platformService;
        _catalogService = catalogService;
        _selectionService = selectionService;
        _detectionService = detectionService;
        _installerService = installerService;
        _loggingService = loggingService;
        _browserService = browserService;
        _settingsService = settingsService;

        Settings = new AppSettings();
        Settings.PropertyChanged += HandleSettingsChanged;

        _installCommand = new AsyncRelayCommand(InstallSelectedAsync, CanInstall);
        _saveListCommand = new RelayCommand(_ => SaveCurrentList());
        _openLogFileCommand = new RelayCommand(_ => OpenLogFile());
        _openPublisherCommand = new RelayCommand(OpenPublisherHomepage);
        _showAppDetailsCommand = new RelayCommand(ShowAppDetails);
        _pauseInstallCommand = new RelayCommand(_ => PauseInstallPlaceholder(), _ => IsInstalling);
        _exportReportCommand = new RelayCommand(_ => ExportReportPlaceholder());
        _toggleSettingsPanelCommand = new RelayCommand(_ => ToggleSettingsPanel());
        _toggleAccountMenuCommand = new RelayCommand(_ => ToggleAccountMenu());
        _openAccountProfileCommand = new RelayCommand(_ => OpenAccountProfile(), _ => !IsInstalling);
        _openAccountSettingsCommand = new RelayCommand(_ => OpenAccountSettings());
        _navigateDashboardCommand = new RelayCommand(_ => NavigateTo(SectionDashboard), _ => !IsInstalling);
        _navigateAppsCommand = new RelayCommand(_ => NavigateTo(SectionApps), _ => !IsInstalling);
        _navigateDriversCommand = new RelayCommand(_ => NavigateTo(SectionDrivers), _ => !IsInstalling);
        _navigateMyListsCommand = new RelayCommand(_ => NavigateTo(SectionMyLists), _ => !IsInstalling);
        _navigateHistoryCommand = new RelayCommand(_ => NavigateTo(SectionHistory), _ => !IsInstalling);
        _navigateLogsCommand = new RelayCommand(_ => NavigateTo(SectionLogs), _ => !IsInstalling);
        _requestRestartNowCommand = new RelayCommand(_ => RequestRestartNow(), _ => CanShowRestartActions());
        _confirmRestartNowCommand = new AsyncRelayCommand(ConfirmRestartNowAsync, CanConfirmRestartNow);
        _cancelRestartCommand = new RelayCommand(_ => CancelRestartNow(), _ => IsRestartConfirmationVisible);
        _restartLaterCommand = new RelayCommand(_ => RestartLater(), _ => CanShowRestartActions());
        _resetSettingsCommand = new AsyncRelayCommand(ResetSettingsAsync, () => !IsInstalling);

        HookCollectionNotifications();
    }

    public AppSettings Settings { get; }

    public string CurrentPlatform
    {
        get => _currentPlatform;
        private set => SetProperty(ref _currentPlatform, value);
    }

    public ObservableCollection<AppItem> Apps => _apps;

    public ObservableCollection<AppItem> VisibleApps => _visibleApps;

    public int SelectedCount => _apps.Count(app => app.IsSelected);

    public int VisibleAppCount => _visibleApps.Count;

    public int RecommendedCount => _apps.Count(app => app.IsRecommended);

    public int UnsupportedSelectedCount => _apps.Count(app => app.WillBeSkipped);

    public bool HasRecommendedApps => RecommendedCount > 0;

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

    public bool IsHomeScreenVisible => !IsInstalling && !IsHistorySelected && !IsLogsSelected;

    public bool IsInstallScreenVisible => IsInstalling;

    public bool IsSummaryScreenVisible => !IsInstalling && IsHistorySelected && HasInstallResults;

    public bool IsHistoryEmptyScreenVisible => !IsInstalling && IsHistorySelected && !HasInstallResults;

    public bool IsLogsScreenVisible => !IsInstalling && IsLogsSelected;

    public bool IsSelectionPhase => !IsHistorySelected && !IsLogsSelected;

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

    public ICommand OpenLogFileCommand => _openLogFileCommand;

    public ICommand OpenPublisherCommand => _openPublisherCommand;

    public ICommand ShowAppDetailsCommand => _showAppDetailsCommand;

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

    public ICommand RequestRestartNowCommand => _requestRestartNowCommand;

    public ICommand ConfirmRestartNowCommand => _confirmRestartNowCommand;

    public ICommand CancelRestartCommand => _cancelRestartCommand;

    public ICommand RestartLaterCommand => _restartLaterCommand;

    public ICommand ResetSettingsCommand => _resetSettingsCommand;

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        await LoadSettingsAsync();

        var platform = _platformService.GetCurrentPlatformInfo();
        _currentPlatformId = platform.Id;
        CurrentPlatform = platform.Label;

        var apps = _catalogService.LoadApps(_currentPlatformId);
        var selection = _selectionService.LoadSelection();
        ApplySelectionProfile(selection);

        if (_settingsService.LoadedDefaultsOnLastLoad)
        {
            ApplySelectionSettingsFallback(selection);
        }

        _selectionService.ApplySelection(apps, selection);

        if (Settings.AutoDetectInstalledAppsOnStartup)
        {
            _detectionService.DetectInstalledApps(apps, _currentPlatformId);
            _installedAppsDetected = true;
        }

        // Recommendation pass runs after platform + catalog + selection are loaded.
        var recommendationSummary = _detectionService.ApplyRecommendations(
            apps,
            _currentPlatformId,
            autoSelectSupportedApps: true);

        if (recommendationSummary.RecommendedAppIds.Count > 0)
        {
            _loggingService.LogInfo(
                $"Recommendations applied. Total={recommendationSummary.RecommendedAppIds.Count}, Supported={recommendationSummary.SupportedRecommendations}, Unsupported={recommendationSummary.UnsupportedRecommendations}");
        }
        else
        {
            _loggingService.LogInfo("No recommendation matches found in catalog for detected hardware/accessories.");
        }

        _apps.Clear();
        foreach (var app in apps)
        {
            ApplyPlatformFlags(app);
            app.PropertyChanged += HandleAppPropertyChanged;
            _apps.Add(app);
        }

        ApplyVisibilityFilter();

        var skippedCount = _apps.Count(app => app.WillBeSkipped);
        var recommendedCount = _apps.Count(app => app.IsRecommended);
        StatusText = skippedCount > 0
            ? $"Loaded {_apps.Count} apps for {CurrentPlatform}. Recommended: {recommendedCount}. {skippedCount} selected app(s) are unsupported and will be skipped."
            : $"Loaded {_apps.Count} apps for {CurrentPlatform}. Recommended: {recommendedCount}.";

        _loggingService.LogInfo(StatusText);
        NotifyAppSummaryStateChanged();
        NotifySettingsOptionBindingsChanged();
        NotifyScreenStateChanged();
        UpdateCommandStates();
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
    }

    private void HandleSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName))
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(AppSettings.OsSupportedApps):
            case nameof(AppSettings.ShowAlreadyInstalledApps):
                ApplyVisibilityFilter();
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

        _detectionService.DetectInstalledApps(appSource, _currentPlatformId);
        foreach (var app in appSource)
        {
            ApplyPlatformFlags(app);
        }

        _installedAppsDetected = true;
        ApplyVisibilityFilter();
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
                var blob = $"{app.Name} {app.Category} {app.PublisherName} {app.Description}";
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
            IsDevToolsFilter = string.Equals(filter, "Dev Tools", StringComparison.OrdinalIgnoreCase);
            IsUtilitiesFilter = string.Equals(filter, "Utilities", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            _updatingFilterFlags = false;
        }

        _loggingService.LogInfo($"Category filter set to '{SelectedFilter}'.");
        ApplyVisibilityFilter();
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
            "Dev Tools" => category.Contains("coding", StringComparison.OrdinalIgnoreCase) ||
                           category.Contains("dev", StringComparison.OrdinalIgnoreCase),
            "Utilities" => category.Contains("util", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private bool MatchesCurrentSection(AppItem app)
    {
        var category = app.Category ?? string.Empty;

        return _currentSection switch
        {
            SectionDrivers => category.Contains("driver", StringComparison.OrdinalIgnoreCase) ||
                              category.Contains("accessor", StringComparison.OrdinalIgnoreCase),
            SectionMyLists => app.IsSelected,
            _ => true
        };
    }

    private void NotifyScreenStateChanged()
    {
        OnPropertyChanged(nameof(IsHomeScreenVisible));
        OnPropertyChanged(nameof(IsInstallScreenVisible));
        OnPropertyChanged(nameof(IsSummaryScreenVisible));
        OnPropertyChanged(nameof(IsHistoryEmptyScreenVisible));
        OnPropertyChanged(nameof(IsLogsScreenVisible));
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

        _loggingService.LogInfo($"Navigation changed to '{section}'.");

        if (section == SectionHistory && !HasInstallResults)
        {
            StatusText = "No install history yet.";
        }
        else if (section == SectionLogs)
        {
            StatusText = "Logs page selected. Use Open Log File to inspect the current log.";
        }

        NotifyNavigationStateChanged();
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

    private void ToggleSettingsPanel()
    {
        IsSettingsPanelOpen = !IsSettingsPanelOpen;
        if (IsSettingsPanelOpen)
        {
            IsAccountMenuOpen = false;
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

        StatusText = $"{app.Name}: {app.Description}";
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

    private void HandleAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not AppItem app || e.PropertyName != nameof(AppItem.IsSelected))
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
            app.HasInstallFailed = false;
            app.StatusBadge = "Installed";
        }
        else if (result.Skipped)
        {
            app.StatusBadge = app.WillBeSkipped ? "Will Be Skipped" : "Skipped";
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
            app.StatusBadge = "Installed";
        }
        else if (!app.HasInstallFailed)
        {
            app.StatusBadge = app.IsSelected ? "Selected" : "Available";
        }

        if (app.RecommendationReason == UnsupportedSelectionNote)
        {
            app.RecommendationReason = string.Empty;
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
        _pauseInstallCommand.RaiseCanExecuteChanged();
        _navigateDashboardCommand.RaiseCanExecuteChanged();
        _navigateAppsCommand.RaiseCanExecuteChanged();
        _navigateDriversCommand.RaiseCanExecuteChanged();
        _navigateMyListsCommand.RaiseCanExecuteChanged();
        _navigateHistoryCommand.RaiseCanExecuteChanged();
        _navigateLogsCommand.RaiseCanExecuteChanged();
        _openAccountProfileCommand.RaiseCanExecuteChanged();
        _openAccountSettingsCommand.RaiseCanExecuteChanged();
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
        OnPropertyChanged(nameof(RecommendedAppsSummary));
        OnPropertyChanged(nameof(UnsupportedSelectedCount));
        OnPropertyChanged(nameof(HasUnsupportedSelectedApps));
    }

    private static bool IsUnsupportedSkippedResult(InstallResult result)
    {
        return result.Skipped &&
               result.Message.Contains("unsupported", StringComparison.OrdinalIgnoreCase);
    }

    public sealed record SettingChoice(string Value, string Label);
}
