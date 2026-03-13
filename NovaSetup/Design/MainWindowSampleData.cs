using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using NovaSetup.Models;
using NovaSetup.ViewModels;

namespace NovaSetup.Design;

public sealed class MainWindowSampleData
{
    public MainWindowSampleData()
    {
        VisibleApps = new ObservableCollection<AppItem>
        {
            CreateApp("firefox", "Mozilla Firefox", "Browsers", "Mozilla", true, false, false, "Selected"),
            CreateApp("steam", "Steam", "Games", "Valve", false, false, false, "Available"),
            CreateApp("nvidia", "NVIDIA App", "Drivers", "NVIDIA", true, true, false, "Installed"),
            CreateApp("vscode", "Visual Studio Code", "Dev Tools", "Microsoft", false, false, false, "Available"),
            CreateApp("git", "Git", "Utilities", "Git Project", false, false, false, "Installed"),
            CreateApp("htop", "htop", "Utilities", "htop dev team", true, false, true, "Will Be Skipped")
        };

        InstallResults = new ObservableCollection<InstallResult>();
        InstalledResults = new ObservableCollection<InstallResult>();
        FailedResults = new ObservableCollection<InstallResult>();
        SkippedResults = new ObservableCollection<InstallResult>();
        RestartRequiredResults = new ObservableCollection<InstallResult>();
        UnsupportedSkippedResults = new ObservableCollection<InstallResult>();

        Settings = new AppSettings();
        RestartBehaviorOptions = [];
        DownloadLocationOptions = [];
        ThemeOptions = [];
        LanguageOptions = [];

        NoOpCommand = new RelayCommand(_ => { });
    }

    public string CurrentProfileName { get; } = "Website Starter";
    public string HomeTitle { get; } = "Choose what to install";
    public string HomeSubtitle { get; } = "Pick apps, drivers, and packs, then press Install";
    public string CurrentPlatform { get; } = "Windows";
    public int SelectedCount { get; } = 3;
    public int RecommendedCount { get; } = 1;
    public bool HasRecommendedApps { get; } = true;
    public bool HasUnsupportedSelectedApps { get; } = true;
    public int UnsupportedSelectedCount { get; } = 1;
    public string RecommendedAppsSummary { get; } = "NVIDIA App • Logitech G HUB";
    public string SelectedFooterText { get; } = "Visible apps: 6 • Selected apps: 3 • Download size: 90 MB";
    public string InstallSummaryText { get; } = "Installed: 2 | Failed: 0 | Skipped: 1 | Restart: 0";
    public string DashboardHistoryText { get; } = "Installed: 2 | Failed: 0 | Skipped: 1 | Restart: 0";
    public string StatusText { get; } = "Ready.";
    public string InstallStatusText { get; } = "Install has not started.";
    public string RestartStatusText { get; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;
    public bool IsDashboardSelected { get; } = false;
    public bool IsDashboardUnselected { get; } = true;
    public bool IsAppsSelected { get; } = true;
    public bool IsAppsUnselected { get; } = false;
    public bool IsMyListsSelected { get; } = false;
    public bool IsMyListsUnselected { get; } = true;
    public bool IsHistorySelected { get; } = false;
    public bool IsHistoryUnselected { get; } = true;
    public bool IsLogsSelected { get; } = false;
    public bool IsLogsUnselected { get; } = true;
    public bool IsHomeScreenVisible { get; } = true;
    public bool IsInstallScreenVisible { get; } = false;
    public bool IsSummaryScreenVisible { get; } = false;
    public bool IsHistoryEmptyScreenVisible { get; } = false;
    public bool IsLogsScreenVisible { get; } = false;
    public bool IsSelectionPhase { get; } = true;
    public bool IsAllFilter { get; set; } = true;
    public bool IsGamesFilter { get; set; }
    public bool IsDriversFilter { get; set; }
    public bool IsDevToolsFilter { get; set; }
    public bool IsUtilitiesFilter { get; set; }
    public bool IsAccountMenuOpen { get; set; }
    public bool IsSettingsPanelOpen { get; set; }
    public bool IsAppDetailsOpen { get; set; }
    public bool RestartRequired { get; } = false;
    public bool IsRestartSectionVisible { get; } = false;
    public bool ShowRestartActions { get; } = false;
    public bool ShowPrimaryRestartActions { get; } = false;
    public bool IsRestartConfirmationVisible { get; } = false;
    public bool HasInstallResults { get; } = false;
    public bool HasInstalledResults { get; } = false;
    public bool HasFailedResults { get; } = false;
    public bool HasSkippedResults { get; } = false;
    public bool HasRestartRequiredResults { get; } = false;
    public bool HasUnsupportedSkippedResults { get; } = false;
    public bool IsDeveloperModeEnabled { get; } = false;
    public bool IsCustomDownloadFolderVisible { get; } = false;
    public double ProgressValue { get; } = 0;
    public AppSettings Settings { get; }
    public AppItem? SelectedDetailApp { get; } = null;
    public string SelectedDetailDescription { get; } = string.Empty;
    public string SelectedDetailPlatformsText { get; } = string.Empty;
    public string SelectedDetailInstallSupportText { get; } = string.Empty;
    public string SelectedDetailSupportStatusText { get; } = string.Empty;
    public bool HasSelectedDetailRecommendation { get; } = false;
    public ObservableCollection<AppItem> VisibleApps { get; }
    public ObservableCollection<InstallResult> InstallResults { get; }
    public ObservableCollection<InstallResult> InstalledResults { get; }
    public ObservableCollection<InstallResult> FailedResults { get; }
    public ObservableCollection<InstallResult> SkippedResults { get; }
    public ObservableCollection<InstallResult> RestartRequiredResults { get; }
    public ObservableCollection<InstallResult> UnsupportedSkippedResults { get; }
    public IReadOnlyList<object> RestartBehaviorOptions { get; }
    public IReadOnlyList<object> DownloadLocationOptions { get; }
    public IReadOnlyList<object> ThemeOptions { get; }
    public IReadOnlyList<object> LanguageOptions { get; }
    public ICommand NoOpCommand { get; }
    public ICommand InstallCommand => NoOpCommand;
    public ICommand SaveListCommand => NoOpCommand;
    public ICommand OpenLogFileCommand => NoOpCommand;
    public ICommand ShowHelpCommand => NoOpCommand;
    public ICommand OpenPublisherCommand => NoOpCommand;
    public ICommand ShowAppDetailsCommand => NoOpCommand;
    public ICommand CloseAppDetailsCommand => NoOpCommand;
    public ICommand PauseInstallCommand => NoOpCommand;
    public ICommand ExportReportCommand => NoOpCommand;
    public ICommand ToggleSettingsPanelCommand => NoOpCommand;
    public ICommand ToggleAccountMenuCommand => NoOpCommand;
    public ICommand OpenAccountProfileCommand => NoOpCommand;
    public ICommand OpenAccountSettingsCommand => NoOpCommand;
    public ICommand NavigateDashboardCommand => NoOpCommand;
    public ICommand NavigateAppsCommand => NoOpCommand;
    public ICommand NavigateDriversCommand => NoOpCommand;
    public ICommand NavigateMyListsCommand => NoOpCommand;
    public ICommand NavigateHistoryCommand => NoOpCommand;
    public ICommand NavigateLogsCommand => NoOpCommand;
    public ICommand RequestRestartNowCommand => NoOpCommand;
    public ICommand ConfirmRestartNowCommand => NoOpCommand;
    public ICommand CancelRestartCommand => NoOpCommand;
    public ICommand RestartLaterCommand => NoOpCommand;
    public ICommand ResetSettingsCommand => NoOpCommand;

    private static AppItem CreateApp(
        string id,
        string name,
        string category,
        string publisher,
        bool isSelected,
        bool isRecommended,
        bool willBeSkipped,
        string status)
    {
        return new AppItem
        {
            Id = id,
            Name = name,
            Category = category,
            PublisherName = publisher,
            HomepageUrl = "https://example.com",
            Description = $"{name} sample description.",
            IsSelected = isSelected,
            IsRecommended = isRecommended,
            WillBeSkipped = willBeSkipped,
            IsSupportedOnCurrentPlatform = !willBeSkipped,
            IsInstalled = status == "Installed",
            StatusBadge = status,
            RecommendationReason = isRecommended ? "Recommended for this system." : string.Empty
        };
    }
}
