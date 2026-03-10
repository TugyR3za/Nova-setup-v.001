using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using NovaSetup.Models;
using NovaSetup.Services;

namespace NovaSetup.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private const string UnsupportedSelectionNote = "Selected from selection.json but unsupported on this OS.";

    private readonly PlatformService _platformService;
    private readonly CatalogService _catalogService;
    private readonly SelectionService _selectionService;
    private readonly DetectionService _detectionService;
    private readonly InstallerService _installerService;
    private readonly LoggingService _loggingService;
    private readonly BrowserService _browserService;

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
    private readonly RelayCommand _requestRestartNowCommand;
    private readonly AsyncRelayCommand _confirmRestartNowCommand;
    private readonly RelayCommand _cancelRestartCommand;
    private readonly RelayCommand _restartLaterCommand;

    private bool _isInitialized;
    private bool _isInstalling;
    private bool _restartRequired;
    private bool _isRestartConfirmationVisible;
    private bool _restartDecisionFinalized;
    private bool _isSettingsPanelOpen;
    private bool _suppressSettingsLogging;
    private bool _updatingFilterFlags;
    private string _currentPlatformId = PlatformService.Unknown;
    private string _currentPlatform = "Unknown OS";
    private string _statusText = "Ready.";
    private string _installStatusText = "Install has not started.";
    private string _installSummaryText = string.Empty;
    private string _restartStatusText = string.Empty;
    private string _searchText = string.Empty;
    private string _selectedFilter = "All";
    private bool _isAllFilter = true;
    private bool _isGamesFilter;
    private bool _isDevToolsFilter;
    private bool _isUtilitiesFilter;
    private double _progressValue;

    public MainWindowViewModel(
        PlatformService platformService,
        CatalogService catalogService,
        SelectionService selectionService,
        DetectionService detectionService,
        InstallerService installerService,
        LoggingService loggingService,
        BrowserService browserService)
    {
        _platformService = platformService;
        _catalogService = catalogService;
        _selectionService = selectionService;
        _detectionService = detectionService;
        _installerService = installerService;
        _loggingService = loggingService;
        _browserService = browserService;

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
        _requestRestartNowCommand = new RelayCommand(_ => RequestRestartNow(), _ => CanShowRestartActions());
        _confirmRestartNowCommand = new AsyncRelayCommand(ConfirmRestartNowAsync, CanConfirmRestartNow);
        _cancelRestartCommand = new RelayCommand(_ => CancelRestartNow(), _ => IsRestartConfirmationVisible);
        _restartLaterCommand = new RelayCommand(_ => RestartLater(), _ => CanShowRestartActions());

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

    public string SelectedFooterText => $"Selected apps: {SelectedCount} • Download size: {SelectedCount * 30} MB";

    public bool IsHomeScreenVisible => !IsInstalling && !HasInstallResults;

    public bool IsInstallScreenVisible => IsInstalling;

    public bool IsSummaryScreenVisible => !IsInstalling && HasInstallResults;

    public bool IsSelectionPhase => !IsSummaryScreenVisible;

    public bool IsSettingsPanelOpen
    {
        get => _isSettingsPanelOpen;
        private set => SetProperty(ref _isSettingsPanelOpen, value);
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

    public ICommand RequestRestartNowCommand => _requestRestartNowCommand;

    public ICommand ConfirmRestartNowCommand => _confirmRestartNowCommand;

    public ICommand CancelRestartCommand => _cancelRestartCommand;

    public ICommand RestartLaterCommand => _restartLaterCommand;

    public void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        var platform = _platformService.GetCurrentPlatformInfo();
        _currentPlatformId = platform.Id;
        CurrentPlatform = platform.Label;

        var apps = _catalogService.LoadApps(_currentPlatformId);
        var selection = _selectionService.LoadSelection();
        ApplySettingsFromSelection(selection);
        _selectionService.ApplySelection(apps, selection);

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
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedFooterText));
        NotifyScreenStateChanged();
        UpdateCommandStates();
    }

    private void HookCollectionNotifications()
    {
        _installResults.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasInstallResults));
            OnPropertyChanged(nameof(IsRestartSectionVisible));
            NotifyScreenStateChanged();
        };

        _installedResults.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasInstalledResults));
        _failedResults.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasFailedResults));
        _skippedResults.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasSkippedResults));
        _restartRequiredResults.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasRestartRequiredResults));
        _unsupportedSkippedResults.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasUnsupportedSkippedResults));
        _visibleApps.CollectionChanged += (_, _) => OnPropertyChanged(nameof(VisibleAppCount));
    }

    private void HandleSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.ShowUnsupportedApps))
        {
            ApplyVisibilityFilter();
            if (!_suppressSettingsLogging)
            {
                _loggingService.LogInfo($"Setting changed: ShowUnsupportedApps={Settings.ShowUnsupportedApps}");
            }

            return;
        }

        if (_suppressSettingsLogging)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(AppSettings.SilentInstallEnabled):
                _loggingService.LogInfo($"Setting changed: SilentInstallEnabled={Settings.SilentInstallEnabled}");
                break;
            case nameof(AppSettings.SelfDeleteEnabled):
                _loggingService.LogInfo($"Setting changed: SelfDeleteEnabled={Settings.SelfDeleteEnabled}");
                break;
            case nameof(AppSettings.DefaultInstallLocation):
                _loggingService.LogInfo($"Setting changed: DefaultInstallLocation='{Settings.DefaultInstallLocation}'");
                break;
            case nameof(AppSettings.ProfileName):
                _loggingService.LogInfo($"Setting changed: ProfileName='{Settings.ProfileName}'");
                break;
        }
    }

    private void ApplySettingsFromSelection(SelectionConfig? selection)
    {
        if (selection is null)
        {
            // Defaults stay active:
            // Silent ON, Self-delete OFF, Show unsupported ON, default location blank.
            return;
        }

        var selectionSettings = selection.Settings ?? new SelectionSettings();
        _suppressSettingsLogging = true;
        try
        {
            Settings.ProfileName = string.IsNullOrWhiteSpace(selection.ProfileName)
                ? Settings.ProfileName
                : selection.ProfileName;
            Settings.SilentInstallEnabled = selectionSettings.SilentInstall;
            Settings.SelfDeleteEnabled = selectionSettings.SelfDelete;
            Settings.ShowUnsupportedApps = selectionSettings.ShowUnsupportedApps;
            Settings.DefaultInstallLocation = selectionSettings.DefaultInstallLocation ?? string.Empty;
        }
        finally
        {
            _suppressSettingsLogging = false;
        }

        _loggingService.LogInfo(
            $"Applied settings from selection.json: Profile='{Settings.ProfileName}', Silent={Settings.SilentInstallEnabled}, SelfDelete={Settings.SelfDeleteEnabled}, ShowUnsupported={Settings.ShowUnsupportedApps}, InstallLocation='{Settings.DefaultInstallLocation}'");
    }

    private void ApplyVisibilityFilter()
    {
        var query = SearchText.Trim();
        _visibleApps.Clear();

        foreach (var app in _apps)
        {
            var includeByPlatform = Settings.ShowUnsupportedApps || app.IsSupportedOnCurrentPlatform || app.IsSelected;
            if (!includeByPlatform)
            {
                continue;
            }

            if (!MatchesSelectedFilter(app))
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

    private void NotifyScreenStateChanged()
    {
        OnPropertyChanged(nameof(IsHomeScreenVisible));
        OnPropertyChanged(nameof(IsInstallScreenVisible));
        OnPropertyChanged(nameof(IsSummaryScreenVisible));
        OnPropertyChanged(nameof(IsSelectionPhase));
    }

    private void SaveCurrentList()
    {
        var selection = new SelectionConfig
        {
            ProfileName = Settings.ProfileName,
            TargetPlatform = _currentPlatformId,
            SelectedApps = _apps.Where(app => app.IsSelected).Select(app => app.Id).ToList(),
            Settings = new SelectionSettings
            {
                SilentInstall = Settings.SilentInstallEnabled,
                SelfDelete = Settings.SelfDeleteEnabled,
                ShowUnsupportedApps = Settings.ShowUnsupportedApps,
                DefaultInstallLocation = Settings.DefaultInstallLocation
            }
        };

        _selectionService.SaveSelection(selection);
        StatusText = $"Saved list '{selection.ProfileName}' with {selection.SelectedApps.Count} app(s).";
        _loggingService.LogInfo(StatusText);
    }

    private void ToggleSettingsPanel()
    {
        IsSettingsPanelOpen = !IsSettingsPanelOpen;
        _loggingService.LogInfo($"Settings panel {(IsSettingsPanelOpen ? "opened" : "closed")}.");
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
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedFooterText));
        UpdateCommandStates();
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
            $"Install settings: Silent={Settings.SilentInstallEnabled}, SelfDelete={Settings.SelfDeleteEnabled}, ShowUnsupported={Settings.ShowUnsupportedApps}, InstallLocation='{Settings.DefaultInstallLocation}'");

        var processedCount = 0;
        foreach (var app in selectedApps)
        {
            InstallStatusText = $"Installing {app.Name} ({processedCount + 1}/{selectedApps.Count})...";
            var batchResults = await _installerService.InstallSelectedAppsAsync(
                new[] { app },
                _currentPlatformId,
                silentInstallEnabled: Settings.SilentInstallEnabled);

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

        if (Settings.SelfDeleteEnabled)
        {
            _loggingService.LogWarning("Self-delete is enabled, but safe placeholder mode is active. No deletion was performed.");
        }

        if (!string.IsNullOrWhiteSpace(Settings.DefaultInstallLocation))
        {
            _loggingService.LogInfo(
                $"Default install location '{Settings.DefaultInstallLocation}' is stored for future installer path overrides.");
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

        if (!app.IsInstalled && !app.HasInstallFailed)
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
        _requestRestartNowCommand.RaiseCanExecuteChanged();
        _confirmRestartNowCommand.RaiseCanExecuteChanged();
        _cancelRestartCommand.RaiseCanExecuteChanged();
        _restartLaterCommand.RaiseCanExecuteChanged();
    }

    private static bool IsUnsupportedSkippedResult(InstallResult result)
    {
        return result.Skipped &&
               result.Message.Contains("unsupported", StringComparison.OrdinalIgnoreCase);
    }
}
