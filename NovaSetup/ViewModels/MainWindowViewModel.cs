using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using NovaSetup.Models;
using NovaSetup.Services;

namespace NovaSetup.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly PlatformService _platformService;
    private readonly CatalogService _catalogService;
    private readonly SelectionService _selectionService;
    private readonly DetectionService _detectionService;
    private readonly InstallerService _installerService;
    private readonly LoggingService _loggingService;
    private readonly BrowserService _browserService;

    private readonly List<AppItem> _catalog = new();
    private readonly ObservableCollection<AppItem> _visibleApps = new();
    private readonly AsyncRelayCommand _installCommand;

    private string _currentPlatform = PlatformService.Unknown;
    private string _searchText = string.Empty;
    private string _selectedCategory = "All Apps";
    private string _selectedProfile = "Default";
    private bool _silentInstallEnabled = true;
    private bool _selfDeleteAfterInstall;
    private bool _showUnsupportedApps;
    private bool _showOtherPlatformApps;
    private bool _isSettingsVisible = true;
    private bool _showRestartOptions;
    private string _defaultInstallLocation = string.Empty;
    private string _statusText = "Ready";
    private string _installSummary = string.Empty;

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

        Categories = new ObservableCollection<string>
        {
            "All Apps",
            "Essentials",
            "Browsers",
            "Gaming",
            "Communication",
            "Coding",
            "Media",
            "Drivers",
            "Utilities",
            "Accessories"
        };

        Profiles = new ObservableCollection<string> { "Default" };
        _installCommand = new AsyncRelayCommand(InstallAsync, CanInstall);

        InstallCommand = _installCommand;
        SaveProfileCommand = new RelayCommand(_ => SaveSelectionProfile());
        OpenPublisherCommand = new RelayCommand(OpenPublisherPage);
        ShowInfoCommand = new RelayCommand(ShowInfo);
        ToggleSettingsCommand = new RelayCommand(_ => IsSettingsVisible = !IsSettingsVisible);
        RestartNowCommand = new RelayCommand(_ => RestartNow());
        SkipRestartCommand = new RelayCommand(_ => SkipRestart());
    }

    public ObservableCollection<string> Categories { get; }

    public ObservableCollection<string> Profiles { get; }

    public ObservableCollection<AppItem> VisibleApps => _visibleApps;

    public ICommand InstallCommand { get; }

    public ICommand SaveProfileCommand { get; }

    public ICommand OpenPublisherCommand { get; }

    public ICommand ShowInfoCommand { get; }

    public ICommand ToggleSettingsCommand { get; }

    public ICommand RestartNowCommand { get; }

    public ICommand SkipRestartCommand { get; }

    public string CurrentPlatformLabel => _platformService.GetPlatformLabel(_currentPlatform);

    public string CurrentPlatformIcon => _platformService.GetPlatformIcon(_currentPlatform);

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

    public bool SilentInstallEnabled
    {
        get => _silentInstallEnabled;
        set => SetProperty(ref _silentInstallEnabled, value);
    }

    public bool SelfDeleteAfterInstall
    {
        get => _selfDeleteAfterInstall;
        set => SetProperty(ref _selfDeleteAfterInstall, value);
    }

    public bool ShowUnsupportedApps
    {
        get => _showUnsupportedApps;
        set
        {
            if (SetProperty(ref _showUnsupportedApps, value))
            {
                ApplyFilters();
            }
        }
    }

    public bool ShowOtherPlatformApps
    {
        get => _showOtherPlatformApps;
        set
        {
            if (SetProperty(ref _showOtherPlatformApps, value))
            {
                ApplyFilters();
            }
        }
    }

    public bool IsSettingsVisible
    {
        get => _isSettingsVisible;
        set => SetProperty(ref _isSettingsVisible, value);
    }

    public bool ShowRestartOptions
    {
        get => _showRestartOptions;
        set => SetProperty(ref _showRestartOptions, value);
    }

    public string DefaultInstallLocation
    {
        get => _defaultInstallLocation;
        set => SetProperty(ref _defaultInstallLocation, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string InstallSummary
    {
        get => _installSummary;
        set => SetProperty(ref _installSummary, value);
    }

    public int SelectedCount => _catalog.Count(app => app.IsSelected);

    public int FilteredCount => _visibleApps.Count;

    public void Initialize()
    {
        _currentPlatform = _platformService.DetectCurrentPlatform();
        OnPropertyChanged(nameof(CurrentPlatformLabel));
        OnPropertyChanged(nameof(CurrentPlatformIcon));

        _catalog.Clear();
        var apps = _catalogService.LoadApps(_currentPlatform);
        foreach (var app in apps)
        {
            app.PropertyChanged += HandleAppPropertyChanged;
            _catalog.Add(app);
        }

        var selection = _selectionService.LoadSelection();
        if (selection is not null)
        {
            ApplySelectionSettings(selection);
            _selectionService.ApplySelection(_catalog, selection);
            if (!string.IsNullOrWhiteSpace(selection.ProfileName) &&
                !Profiles.Contains(selection.ProfileName))
            {
                Profiles.Add(selection.ProfileName);
                SelectedProfile = selection.ProfileName;
            }
        }

        _detectionService.RunDetections(_catalog, _currentPlatform);
        RefreshDerivedStates();
        ApplyFilters();

        StatusText = $"Loaded {_catalog.Count} apps for {CurrentPlatformLabel}.";
    }

    private void HandleAppPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is not AppItem app)
        {
            return;
        }

        if (args.PropertyName is nameof(AppItem.IsSelected)
            or nameof(AppItem.IsInstalled)
            or nameof(AppItem.HasInstallFailed))
        {
            UpdateAppState(app);
            OnPropertyChanged(nameof(SelectedCount));
            ApplyFilters();
            _installCommand.RaiseCanExecuteChanged();
        }
    }

    private void RefreshDerivedStates()
    {
        foreach (var app in _catalog)
        {
            UpdateAppState(app);
        }

        OnPropertyChanged(nameof(SelectedCount));
        _installCommand.RaiseCanExecuteChanged();
    }

    private void UpdateAppState(AppItem app)
    {
        app.WillBeSkipped = app.IsSelected && !app.IsSupportedOnCurrentPlatform;
        app.RowOpacity = app.IsSupportedOnCurrentPlatform ? 1.0 : 0.56;

        var installDefinition = _currentPlatform == PlatformService.Windows ? app.WindowsInstall : app.LinuxInstall;
        var needsManual = installDefinition?.NeedsManualInstall ?? false;

        if (app.HasInstallFailed)
        {
            app.StatusBadge = "Failed";
            return;
        }

        if (app.IsInstalled && app.RequiresRestartHint)
        {
            app.StatusBadge = "Pending Restart";
            return;
        }

        if (app.IsInstalled)
        {
            app.StatusBadge = "Installed";
            return;
        }

        if (app.WillBeSkipped)
        {
            app.StatusBadge = "Will Be Skipped";
            return;
        }

        if (!app.IsSupportedOnCurrentPlatform)
        {
            app.StatusBadge = "Unsupported on this OS";
            return;
        }

        app.StatusBadge = needsManual ? "Needs Manual Install" : "Not Installed";
    }

    private void ApplyFilters()
    {
        var query = _catalog.AsEnumerable();

        if (!string.Equals(SelectedCategory, "All Apps", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(app => app.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(app =>
                app.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                app.PublisherName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                app.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        if (!ShowUnsupportedApps && !ShowOtherPlatformApps)
        {
            query = query.Where(app => app.IsSupportedOnCurrentPlatform || app.IsSelected);
        }

        var ordered = query
            .OrderByDescending(app => app.IsSelected)
            .ThenByDescending(app => app.IsRecommended)
            .ThenBy(app => app.Name)
            .ToList();

        _visibleApps.Clear();
        foreach (var app in ordered)
        {
            _visibleApps.Add(app);
        }

        OnPropertyChanged(nameof(FilteredCount));
        OnPropertyChanged(nameof(SelectedCount));
        _installCommand.RaiseCanExecuteChanged();
    }

    private bool CanInstall()
    {
        return _catalog.Any(app => app.IsSelected);
    }

    private async Task InstallAsync()
    {
        StatusText = "Installing selected apps...";
        InstallSummary = string.Empty;

        var results = await _installerService.InstallSelectedAppsAsync(
            _catalog,
            _currentPlatform,
            SilentInstallEnabled,
            DefaultInstallLocation);

        var installedCount = results.Count(result => result.Success);
        var failedCount = results.Count(result => !result.Success && !result.Skipped);
        var skippedCount = results.Count(result => result.Skipped);

        InstallSummary = $"Installed: {installedCount} | Failed: {failedCount} | Skipped: {skippedCount}";

        if (skippedCount > 0)
        {
            StatusText = "Install complete. Unsupported selections were skipped.";
        }
        else
        {
            StatusText = "Install complete.";
        }

        ShowRestartOptions = results.Any(result => result.RequiresRestart);
        if (SelfDeleteAfterInstall)
        {
            _loggingService.Warn("Self-delete is enabled. This prototype does not execute self-delete.");
        }

        RefreshDerivedStates();
        ApplyFilters();
    }

    private void SaveSelectionProfile()
    {
        var profileName = string.IsNullOrWhiteSpace(SelectedProfile) ? "Default" : SelectedProfile.Trim();
        if (!Profiles.Contains(profileName))
        {
            Profiles.Add(profileName);
        }

        var selection = new SelectionConfig
        {
            ProfileName = profileName,
            TargetPlatform = _currentPlatform,
            SelectedApps = _catalog.Where(app => app.IsSelected).Select(app => app.Id).ToList(),
            Settings = new SelectionSettings
            {
                SilentInstall = SilentInstallEnabled,
                SelfDelete = SelfDeleteAfterInstall,
                DefaultInstallLocation = DefaultInstallLocation,
                ShowUnsupportedApps = ShowUnsupportedApps,
                ShowOtherPlatforms = ShowOtherPlatformApps
            }
        };

        _selectionService.SaveSelection(selection);
        StatusText = "Selection profile saved.";
    }

    private void OpenPublisherPage(object? parameter)
    {
        if (parameter is not AppItem app)
        {
            return;
        }

        if (!_browserService.OpenPublisherHomepage(app.HomepageUrl))
        {
            StatusText = $"Could not open publisher page for {app.Name}.";
            return;
        }

        StatusText = $"Opened publisher page for {app.Name}.";
    }

    private void ShowInfo(object? parameter)
    {
        if (parameter is not AppItem app)
        {
            return;
        }

        StatusText = $"{app.Name}: {app.Description}";
    }

    private void RestartNow()
    {
        ShowRestartOptions = false;
        StatusText = "Restart requested. Please restart the computer manually.";
        _loggingService.Info("Restart requested by user.");
    }

    private void SkipRestart()
    {
        ShowRestartOptions = false;
        StatusText = "Restart skipped.";
        _loggingService.Info("Restart skipped by user.");
    }

    private void ApplySelectionSettings(SelectionConfig selection)
    {
        var settings = selection.Settings ?? new SelectionSettings();
        SilentInstallEnabled = settings.SilentInstall;
        SelfDeleteAfterInstall = settings.SelfDelete;
        DefaultInstallLocation = settings.DefaultInstallLocation ?? string.Empty;
        ShowUnsupportedApps = settings.ShowUnsupportedApps;
        ShowOtherPlatformApps = settings.ShowOtherPlatforms;
    }
}
