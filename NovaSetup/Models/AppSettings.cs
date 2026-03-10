namespace NovaSetup.Models;

public sealed class AppSettings : ObservableObject
{
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

    private bool _silentInstall = true;
    private bool _osSupportedApps = true;
    private bool _selfDeleteAfterInstall;
    private bool _parallelInstall = true;
    private string _restartBehavior = RestartAskBeforeRestart;
    private bool _showAlreadyInstalledApps = true;
    private bool _autoDetectInstalledAppsOnStartup = true;
    private string _downloadLocationMode = DownloadSystemDefault;
    private string _customDownloadFolder = string.Empty;
    private bool _keepInstallersAfterInstall;
    private bool _launchNovaAtStartup;
    private string _theme = ThemeDark;
    private string _language = LanguageEnglish;
    private bool _saveProfilesAutomatically = true;
    private bool _developerMode;

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
            DownloadLocationMode = DownloadLocationMode,
            CustomDownloadFolder = CustomDownloadFolder,
            KeepInstallersAfterInstall = KeepInstallersAfterInstall,
            LaunchNovaAtStartup = LaunchNovaAtStartup,
            Theme = Theme,
            Language = Language,
            SaveProfilesAutomatically = SaveProfilesAutomatically,
            DeveloperMode = DeveloperMode
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
        DownloadLocationMode = source.DownloadLocationMode;
        CustomDownloadFolder = source.CustomDownloadFolder;
        KeepInstallersAfterInstall = source.KeepInstallersAfterInstall;
        LaunchNovaAtStartup = source.LaunchNovaAtStartup;
        Theme = source.Theme;
        Language = source.Language;
        SaveProfilesAutomatically = source.SaveProfilesAutomatically;
        DeveloperMode = source.DeveloperMode;
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
}
