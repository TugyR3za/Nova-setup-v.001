using System.IO;

namespace NovaSetup.Models;

public sealed class AppSettings : ObservableObject
{
    private static readonly string PortableAppsDefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "PortableApps");

    public const string RestartAskBeforeRestart = "AskBeforeRestart";
    public const string RestartAutomatically = "RestartAutomatically";
    public const string RestartNever = "NeverRestart";

    public const string DownloadSystemDefault = "SystemDefault";
    public const string DownloadCustomFolder = "CustomFolder";
    public const string DownloadAskEveryTime = "AskEveryTime";

    public const string ThemeDark = "Dark";
    public const string ThemeLight = "Light";
    public const string ThemeSystem = "System";

    public const string LanguageEnglish = "English";
    public const string LanguageFarsi = "Farsi";
    public const string LanguageTurkish = "Turkish";

    public const string ScheduledFrequencyDaily = "daily";
    public const string ScheduledFrequencyWeekly = "weekly";
    public const string ScheduledFrequencyMonthly = "monthly";

    private bool _silentInstall = true;
    private bool _osSupportedApps = true;
    private bool _selfDeleteAfterInstall;
    private bool _parallelInstall = true;
    private string _restartBehavior = RestartAskBeforeRestart;
    private bool _showAlreadyInstalledApps = true;
    private bool _autoDetectInstalledAppsOnStartup = true;
    private bool _autoSelectDrivers;
    private string _downloadLocationMode = DownloadSystemDefault;
    private string _customDownloadFolder = string.Empty;
    private bool _keepInstallersAfterInstall;
    private bool _launchNovaAtStartup;
    private bool _checkForUpdatesAutomatically = true;
    private string _theme = ThemeDark;
    private string _language = LanguageEnglish;
    private bool _saveProfilesAutomatically = true;
    private bool _developerMode;
    private bool _scheduledUpdatesEnabled;
    private string _scheduledUpdateFrequency = ScheduledFrequencyWeekly;
    private int _scheduledUpdateHour = 3;
    private DayOfWeek _scheduledUpdateDay = DayOfWeek.Sunday;
    private DateTime _lastScheduledUpdateRun = DateTime.MinValue;
    private bool _runMissedUpdatesASAP = true;
    private string _defaultPortableFolder = PortableAppsDefaultPath;
    private bool _allowScriptExecution;

    public bool SilentInstall
    {
        get => _silentInstall;
        set => SetProperty(ref _silentInstall, value);
    }

    public bool OsSupportedApps
    {
        get => _osSupportedApps;
        set => SetProperty(ref _osSupportedApps, value);
    }

    public bool SelfDeleteAfterInstall
    {
        get => _selfDeleteAfterInstall;
        set => SetProperty(ref _selfDeleteAfterInstall, value);
    }

    public bool ParallelInstall
    {
        get => _parallelInstall;
        set => SetProperty(ref _parallelInstall, value);
    }

    public string RestartBehavior
    {
        get => _restartBehavior;
        set => SetProperty(ref _restartBehavior, NormalizeRestartBehavior(value));
    }

    public bool ShowAlreadyInstalledApps
    {
        get => _showAlreadyInstalledApps;
        set => SetProperty(ref _showAlreadyInstalledApps, value);
    }

    public bool AutoDetectInstalledAppsOnStartup
    {
        get => _autoDetectInstalledAppsOnStartup;
        set => SetProperty(ref _autoDetectInstalledAppsOnStartup, value);
    }

    public bool AutoSelectDrivers
    {
        get => _autoSelectDrivers;
        set => SetProperty(ref _autoSelectDrivers, value);
    }

    public string DownloadLocationMode
    {
        get => _downloadLocationMode;
        set => SetProperty(ref _downloadLocationMode, NormalizeDownloadLocationMode(value));
    }

    public string CustomDownloadFolder
    {
        get => _customDownloadFolder;
        set => SetProperty(ref _customDownloadFolder, value ?? string.Empty);
    }

    public bool KeepInstallersAfterInstall
    {
        get => _keepInstallersAfterInstall;
        set => SetProperty(ref _keepInstallersAfterInstall, value);
    }

    public bool LaunchNovaAtStartup
    {
        get => _launchNovaAtStartup;
        set => SetProperty(ref _launchNovaAtStartup, value);
    }

    public bool CheckForUpdatesAutomatically
    {
        get => _checkForUpdatesAutomatically;
        set => SetProperty(ref _checkForUpdatesAutomatically, value);
    }

    public string Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, NormalizeTheme(value));
    }

    public string Language
    {
        get => _language;
        set => SetProperty(ref _language, NormalizeLanguage(value));
    }

    public bool SaveProfilesAutomatically
    {
        get => _saveProfilesAutomatically;
        set => SetProperty(ref _saveProfilesAutomatically, value);
    }

    public bool DeveloperMode
    {
        get => _developerMode;
        set => SetProperty(ref _developerMode, value);
    }

    public bool ScheduledUpdatesEnabled
    {
        get => _scheduledUpdatesEnabled;
        set => SetProperty(ref _scheduledUpdatesEnabled, value);
    }

    public string ScheduledUpdateFrequency
    {
        get => _scheduledUpdateFrequency;
        set => SetProperty(ref _scheduledUpdateFrequency, NormalizeScheduledUpdateFrequency(value));
    }

    public int ScheduledUpdateHour
    {
        get => _scheduledUpdateHour;
        set => SetProperty(ref _scheduledUpdateHour, Math.Clamp(value, 0, 23));
    }

    public DayOfWeek ScheduledUpdateDay
    {
        get => _scheduledUpdateDay;
        set => SetProperty(ref _scheduledUpdateDay, value);
    }

    public DateTime LastScheduledUpdateRun
    {
        get => _lastScheduledUpdateRun;
        set => SetProperty(ref _lastScheduledUpdateRun, value);
    }

    public bool RunMissedUpdatesASAP
    {
        get => _runMissedUpdatesASAP;
        set => SetProperty(ref _runMissedUpdatesASAP, value);
    }

    public string DefaultPortableFolder
    {
        get => _defaultPortableFolder;
        set => SetProperty(
            ref _defaultPortableFolder,
            string.IsNullOrWhiteSpace(value) ? PortableAppsDefaultPath : value);
    }

    public bool AllowScriptExecution
    {
        get => _allowScriptExecution;
        set => SetProperty(ref _allowScriptExecution, value);
    }

    public static AppSettings CreateDefault()
    {
        return new AppSettings();
    }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            SilentInstall = SilentInstall,
            OsSupportedApps = OsSupportedApps,
            SelfDeleteAfterInstall = SelfDeleteAfterInstall,
            ParallelInstall = ParallelInstall,
            RestartBehavior = RestartBehavior,
            ShowAlreadyInstalledApps = ShowAlreadyInstalledApps,
            AutoDetectInstalledAppsOnStartup = AutoDetectInstalledAppsOnStartup,
            AutoSelectDrivers = AutoSelectDrivers,
            DownloadLocationMode = DownloadLocationMode,
            CustomDownloadFolder = CustomDownloadFolder,
            KeepInstallersAfterInstall = KeepInstallersAfterInstall,
            LaunchNovaAtStartup = LaunchNovaAtStartup,
            CheckForUpdatesAutomatically = CheckForUpdatesAutomatically,
            Theme = Theme,
            Language = Language,
            SaveProfilesAutomatically = SaveProfilesAutomatically,
            DeveloperMode = DeveloperMode,
            ScheduledUpdatesEnabled = ScheduledUpdatesEnabled,
            ScheduledUpdateFrequency = ScheduledUpdateFrequency,
            ScheduledUpdateHour = ScheduledUpdateHour,
            ScheduledUpdateDay = ScheduledUpdateDay,
            LastScheduledUpdateRun = LastScheduledUpdateRun,
            RunMissedUpdatesASAP = RunMissedUpdatesASAP,
            DefaultPortableFolder = DefaultPortableFolder,
            AllowScriptExecution = AllowScriptExecution
        };
    }

    public void ApplyFrom(AppSettings? source)
    {
        source ??= CreateDefault();

        SilentInstall = source.SilentInstall;
        OsSupportedApps = source.OsSupportedApps;
        SelfDeleteAfterInstall = source.SelfDeleteAfterInstall;
        ParallelInstall = source.ParallelInstall;
        RestartBehavior = source.RestartBehavior;
        ShowAlreadyInstalledApps = source.ShowAlreadyInstalledApps;
        AutoDetectInstalledAppsOnStartup = source.AutoDetectInstalledAppsOnStartup;
        AutoSelectDrivers = source.AutoSelectDrivers;
        DownloadLocationMode = source.DownloadLocationMode;
        CustomDownloadFolder = source.CustomDownloadFolder;
        KeepInstallersAfterInstall = source.KeepInstallersAfterInstall;
        LaunchNovaAtStartup = source.LaunchNovaAtStartup;
        CheckForUpdatesAutomatically = source.CheckForUpdatesAutomatically;
        Theme = source.Theme;
        Language = source.Language;
        SaveProfilesAutomatically = source.SaveProfilesAutomatically;
        DeveloperMode = source.DeveloperMode;
        ScheduledUpdatesEnabled = source.ScheduledUpdatesEnabled;
        ScheduledUpdateFrequency = source.ScheduledUpdateFrequency;
        ScheduledUpdateHour = source.ScheduledUpdateHour;
        ScheduledUpdateDay = source.ScheduledUpdateDay;
        LastScheduledUpdateRun = source.LastScheduledUpdateRun;
        RunMissedUpdatesASAP = source.RunMissedUpdatesASAP;
        DefaultPortableFolder = source.DefaultPortableFolder;
        AllowScriptExecution = source.AllowScriptExecution;
    }

    private static string NormalizeRestartBehavior(string? value)
    {
        return value switch
        {
            RestartAskBeforeRestart => RestartAskBeforeRestart,
            RestartAutomatically => RestartAutomatically,
            RestartNever => RestartNever,
            _ => RestartAskBeforeRestart
        };
    }

    private static string NormalizeDownloadLocationMode(string? value)
    {
        return value switch
        {
            DownloadSystemDefault => DownloadSystemDefault,
            DownloadCustomFolder => DownloadCustomFolder,
            DownloadAskEveryTime => DownloadAskEveryTime,
            _ => DownloadSystemDefault
        };
    }

    private static string NormalizeTheme(string? value)
    {
        return value switch
        {
            ThemeDark => ThemeDark,
            ThemeLight => ThemeLight,
            ThemeSystem => ThemeSystem,
            _ => ThemeDark
        };
    }

    private static string NormalizeLanguage(string? value)
    {
        return value switch
        {
            LanguageEnglish => LanguageEnglish,
            LanguageFarsi => LanguageFarsi,
            LanguageTurkish => LanguageTurkish,
            _ => LanguageEnglish
        };
    }

    private static string NormalizeScheduledUpdateFrequency(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            ScheduledFrequencyDaily => ScheduledFrequencyDaily,
            ScheduledFrequencyWeekly => ScheduledFrequencyWeekly,
            ScheduledFrequencyMonthly => ScheduledFrequencyMonthly,
            _ => ScheduledFrequencyWeekly
        };
    }
}
