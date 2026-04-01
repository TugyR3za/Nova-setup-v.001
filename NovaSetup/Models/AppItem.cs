using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows.Input;
using NovaSetup.Services;

namespace NovaSetup.Models;

public class AppItem : ObservableObject
{
    public const string StatusNotInstalled = "Not Installed";
    public const string StatusAvailable = "Available";
    public const string StatusSelected = "Selected";
    public const string StatusInstalled = "Installed";
    public const string StatusInstalling = "Installing";
    public const string StatusUpdateAvailable = "Update Available";
    public const string StatusWillBeSkipped = "Will Be Skipped";
    public const string StatusUnsupportedOnCurrentOs = "Not available on this platform";
    public const string StatusNeedsManualInstall = "Needs Manual Install";
    public const string StatusSkipped = "Skipped";
    public const string StatusFailed = "Failed";
    public const string StatusCancelled = "Cancelled";

    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _category = string.Empty;
    private string _publisherName = string.Empty;
    private string _homepageUrl = string.Empty;
    private string _description = string.Empty;
    private string _iconPath = string.Empty;
    private string? _logoUrl;
    private string _wingetId = string.Empty;
    private string _version = string.Empty;
    private string _installedVersion = string.Empty;
    private string _license = string.Empty;
    private string _releaseNotesUrl = string.Empty;
    private List<string> _dependencies = new();
    private bool _isPortable;
    private PlatformSupport _supportedPlatforms = new();
    private InstallDefinition? _windowsInstall;
    private InstallDefinition? _linuxInstall;
    private InstallDefinition? _macOSInstall;
    private bool _isSelected;
    private bool _isInstalled;
    private bool _isSupportedOnCurrentPlatform = true;
    private bool _willBeSkipped;
    private bool _isRecommended;
    private string _recommendationReason = string.Empty;
    private bool _requiresRestartHint;
    private bool _supportsSilentInstall;
    private bool _isHidden;
    private List<string> _recommendationTags = new();
    private List<string> _tags = new();

    // Runtime/UI state (not part of apps.json schema).
    private bool _supportsCustomPath;
    private bool _hasInstallFailed;
    private string _statusBadge = StatusNotInstalled;
    private double _rowOpacity = 1.0;
    private bool _isCancellable;
    private ICommand? _cancelCommand;
    private string _portableInstallPath = string.Empty;
    private long _downloadedBytes;
    private long _totalBytes;
    private string _downloadProgressText = string.Empty;
    private double _downloadProgressPercent;
    private bool _userDisabledSilentInstall;
    private bool _userDisabledScanning;
    private bool _userDisabledAutoUpdate;
    private bool _userTrustedInstallScripts;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                OnPropertyChanged(nameof(IconGlyph));
            }
        }
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public string PublisherName
    {
        get => _publisherName;
        set => SetProperty(ref _publisherName, value);
    }

    public string HomepageUrl
    {
        get => _homepageUrl;
        set
        {
            if (SetProperty(ref _homepageUrl, value))
            {
                OnPropertyChanged(nameof(HasHomepageLink));
            }
        }
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string IconPath
    {
        get => _iconPath;
        set => SetProperty(ref _iconPath, value);
    }

    [JsonPropertyName("logoUrl")]
    public string? LogoUrl
    {
        get => _logoUrl;
        set => SetProperty(
            ref _logoUrl,
            string.IsNullOrWhiteSpace(value) ? null : value.Trim());
    }

    public string WingetId
    {
        get => _wingetId;
        set => SetProperty(ref _wingetId, value);
    }

    public string Version
    {
        get => _version;
        set
        {
            if (SetProperty(ref _version, value))
            {
                OnPropertyChanged(nameof(HasUpdateAvailable));
            }
        }
    }

    [JsonIgnore]
    public string InstalledVersion
    {
        get => _installedVersion;
        set
        {
            if (SetProperty(ref _installedVersion, value))
            {
                OnPropertyChanged(nameof(HasUpdateAvailable));
            }
        }
    }

    public string License
    {
        get => _license;
        set => SetProperty(ref _license, value);
    }

    public string ReleaseNotesUrl
    {
        get => _releaseNotesUrl;
        set => SetProperty(ref _releaseNotesUrl, value);
    }

    public List<string> Tags
    {
        get => _tags;
        set => SetProperty(ref _tags, value ?? new List<string>());
    }

    public List<string> Dependencies
    {
        get => _dependencies;
        set => SetProperty(ref _dependencies, value ?? new List<string>());
    }

    public bool IsPortable
    {
        get => _isPortable;
        set => SetProperty(ref _isPortable, value);
    }

    public PlatformSupport SupportedPlatforms
    {
        get => _supportedPlatforms;
        set => SetProperty(ref _supportedPlatforms, value);
    }

    public InstallDefinition? WindowsInstall
    {
        get => _windowsInstall;
        set
        {
            if (SetProperty(ref _windowsInstall, value))
            {
                OnPropertyChanged(nameof(ShowArmIndicator));
                OnPropertyChanged(nameof(ArmIndicatorText));
                OnPropertyChanged(nameof(CurrentVirusTotalUrl));
                OnPropertyChanged(nameof(CurrentVirusTotalRatio));
                OnPropertyChanged(nameof(HasVirusTotalData));
                OnPropertyChanged(nameof(VirusTotalDisplayText));
                OnPropertyChanged(nameof(HasInstallScripts));
                OnPropertyChanged(nameof(CanShowPreferencesButton));
                OnPropertyChanged(nameof(InstallScriptsPreferenceMenuText));
            }
        }
    }

    public InstallDefinition? LinuxInstall
    {
        get => _linuxInstall;
        set
        {
            if (SetProperty(ref _linuxInstall, value))
            {
                OnPropertyChanged(nameof(HasInstallScripts));
                OnPropertyChanged(nameof(CanShowPreferencesButton));
                OnPropertyChanged(nameof(InstallScriptsPreferenceMenuText));
            }
        }
    }

    public InstallDefinition? MacOSInstall
    {
        get => _macOSInstall;
        set
        {
            if (SetProperty(ref _macOSInstall, value))
            {
                OnPropertyChanged(nameof(HasInstallScripts));
                OnPropertyChanged(nameof(CanShowPreferencesButton));
                OnPropertyChanged(nameof(InstallScriptsPreferenceMenuText));
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsInstalled
    {
        get => _isInstalled;
        set
        {
            if (SetProperty(ref _isInstalled, value))
            {
                OnPropertyChanged(nameof(HasUpdateAvailable));
                OnPropertyChanged(nameof(CanInstallFromContextMenu));
                OnPropertyChanged(nameof(CanShowPreferencesButton));
            }
        }
    }

    public bool IsSupportedOnCurrentPlatform
    {
        get => _isSupportedOnCurrentPlatform;
        set => SetProperty(ref _isSupportedOnCurrentPlatform, value);
    }

    public bool WillBeSkipped
    {
        get => _willBeSkipped;
        set => SetProperty(ref _willBeSkipped, value);
    }

    public bool IsRecommended
    {
        get => _isRecommended;
        set => SetProperty(ref _isRecommended, value);
    }

    public string RecommendationReason
    {
        get => _recommendationReason;
        set => SetProperty(ref _recommendationReason, value);
    }

    public bool RequiresRestartHint
    {
        get => _requiresRestartHint;
        set => SetProperty(ref _requiresRestartHint, value);
    }

    public bool SupportsSilentInstall
    {
        get => _supportsSilentInstall;
        set => SetProperty(ref _supportsSilentInstall, value);
    }

    public bool IsHidden
    {
        get => _isHidden;
        set => SetProperty(ref _isHidden, value);
    }

    public List<string> RecommendationTags
    {
        get => _recommendationTags;
        set => SetProperty(ref _recommendationTags, value ?? new List<string>());
    }

    [JsonIgnore]
    public bool SupportsCustomPath
    {
        get => _supportsCustomPath;
        set => SetProperty(ref _supportsCustomPath, value);
    }

    [JsonIgnore]
    public bool HasInstallFailed
    {
        get => _hasInstallFailed;
        set => SetProperty(ref _hasInstallFailed, value);
    }

    [JsonIgnore]
    public string StatusBadge
    {
        get => _statusBadge;
        set => SetProperty(ref _statusBadge, value);
    }

    [JsonIgnore]
    public double RowOpacity
    {
        get => _rowOpacity;
        set => SetProperty(ref _rowOpacity, value);
    }

    [JsonIgnore]
    public bool IsCancellable
    {
        get => _isCancellable;
        set => SetProperty(ref _isCancellable, value);
    }

    [JsonIgnore]
    public ICommand? CancelCommand
    {
        get => _cancelCommand;
        set => SetProperty(ref _cancelCommand, value);
    }

    [JsonIgnore]
    public string PortableInstallPath
    {
        get => _portableInstallPath;
        set => SetProperty(ref _portableInstallPath, value ?? string.Empty);
    }

    [JsonIgnore]
    public long DownloadedBytes
    {
        get => _downloadedBytes;
        set => SetProperty(ref _downloadedBytes, value);
    }

    [JsonIgnore]
    public long TotalBytes
    {
        get => _totalBytes;
        set => SetProperty(ref _totalBytes, value);
    }

    [JsonIgnore]
    public string DownloadProgressText
    {
        get => _downloadProgressText;
        set => SetProperty(ref _downloadProgressText, value ?? string.Empty);
    }

    [JsonIgnore]
    public double DownloadProgressPercent
    {
        get => _downloadProgressPercent;
        set => SetProperty(ref _downloadProgressPercent, value);
    }

    [JsonIgnore]
    public bool UserDisabledSilentInstall
    {
        get => _userDisabledSilentInstall;
        set
        {
            if (SetProperty(ref _userDisabledSilentInstall, value))
            {
                OnPropertyChanged(nameof(SilentInstallEnabled));
                OnPropertyChanged(nameof(SilentInstallPreferenceMenuText));
            }
        }
    }

    [JsonIgnore]
    public bool UserDisabledScanning
    {
        get => _userDisabledScanning;
        set
        {
            if (SetProperty(ref _userDisabledScanning, value))
            {
                OnPropertyChanged(nameof(ScanningEnabled));
                OnPropertyChanged(nameof(UpdateScanningPreferenceMenuText));
            }
        }
    }

    [JsonIgnore]
    public bool UserDisabledAutoUpdate
    {
        get => _userDisabledAutoUpdate;
        set
        {
            if (SetProperty(ref _userDisabledAutoUpdate, value))
            {
                OnPropertyChanged(nameof(AutoUpdateEnabled));
            }
        }
    }

    [JsonIgnore]
    public bool UserTrustedInstallScripts
    {
        get => _userTrustedInstallScripts;
        set
        {
            if (SetProperty(ref _userTrustedInstallScripts, value))
            {
                OnPropertyChanged(nameof(InstallScriptsEnabled));
                OnPropertyChanged(nameof(InstallScriptsPreferenceMenuText));
            }
        }
    }

    [JsonIgnore]
    public bool SilentInstallEnabled
    {
        get => !UserDisabledSilentInstall;
        set => UserDisabledSilentInstall = !value;
    }

    [JsonIgnore]
    public bool ScanningEnabled
    {
        get => !UserDisabledScanning;
        set => UserDisabledScanning = !value;
    }

    [JsonIgnore]
    public bool AutoUpdateEnabled
    {
        get => !UserDisabledAutoUpdate;
        set => UserDisabledAutoUpdate = !value;
    }

    [JsonIgnore]
    public bool InstallScriptsEnabled
    {
        get => UserTrustedInstallScripts;
        set => UserTrustedInstallScripts = value;
    }

    [JsonIgnore]
    public bool HasHomepageLink => !string.IsNullOrWhiteSpace(HomepageUrl);

    [JsonIgnore]
    public bool CanInstallFromContextMenu => !IsInstalled;

    [JsonIgnore]
    public string SilentInstallPreferenceMenuText =>
        UserDisabledSilentInstall
            ? "Enable silent install"
            : "Disable silent install";

    [JsonIgnore]
    public string UpdateScanningPreferenceMenuText =>
        UserDisabledScanning
            ? "Enable update scanning"
            : "Skip update scanning";

    [JsonIgnore]
    public bool HasInstallScripts =>
        !string.IsNullOrWhiteSpace(WindowsInstall?.PreInstallScript) ||
        !string.IsNullOrWhiteSpace(WindowsInstall?.PostInstallScript) ||
        !string.IsNullOrWhiteSpace(LinuxInstall?.PreInstallScript) ||
        !string.IsNullOrWhiteSpace(LinuxInstall?.PostInstallScript) ||
        !string.IsNullOrWhiteSpace(MacOSInstall?.PreInstallScript) ||
        !string.IsNullOrWhiteSpace(MacOSInstall?.PostInstallScript);

    [JsonIgnore]
    public bool CanShowPreferencesButton => IsInstalled || HasInstallScripts;

    [JsonIgnore]
    public string InstallScriptsPreferenceMenuText =>
        UserTrustedInstallScripts
            ? "Disallow install scripts"
            : "Allow install scripts";

    [JsonIgnore]
    public bool IsArm64Machine => PlatformService.IsArm64();

    [JsonIgnore]
    public bool ShowArmIndicator => IsArm64Machine && (WindowsInstall?.HasArm64Support ?? false);

    [JsonIgnore]
    public string ArmIndicatorText => ShowArmIndicator ? "ARM64" : string.Empty;

    [JsonIgnore]
    public string IconGlyph => string.IsNullOrWhiteSpace(Name) ? "?" : Name[0].ToString().ToUpperInvariant();

    [JsonIgnore]
    public string CurrentVirusTotalUrl => WindowsInstall?.VirusTotalUrl ?? string.Empty;

    [JsonIgnore]
    public string CurrentVirusTotalRatio => WindowsInstall?.VirusTotalRatio ?? string.Empty;

    [JsonIgnore]
    public bool HasVirusTotalData => !string.IsNullOrWhiteSpace(CurrentVirusTotalRatio);

    [JsonIgnore]
    public string VirusTotalDisplayText => HasVirusTotalData ? $"VT: {CurrentVirusTotalRatio}" : string.Empty;

    [JsonIgnore]
    public bool HasUpdateAvailable =>
        IsInstalled &&
        IsCatalogVersionNewer(Version, InstalledVersion);

    private static bool IsCatalogVersionNewer(string catalogVersionText, string installedVersionText)
    {
        if (string.IsNullOrWhiteSpace(catalogVersionText) || string.IsNullOrWhiteSpace(installedVersionText))
        {
            return false;
        }

        if (System.Version.TryParse(catalogVersionText.Trim(), out var catalogVersion) &&
            catalogVersion is not null &&
            System.Version.TryParse(installedVersionText.Trim(), out var installedVersion) &&
            installedVersion is not null)
        {
            if (catalogVersion.Major != installedVersion.Major)
            {
                return catalogVersion.Major > installedVersion.Major;
            }

            if (catalogVersion.Minor != installedVersion.Minor)
            {
                return catalogVersion.Minor > installedVersion.Minor;
            }

            return false;
        }

        return !string.Equals(
            catalogVersionText.Trim(),
            installedVersionText.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }
}

public class InstallDefinition
{
    // Optional direct installer URL for download-first install flows.
    public string InstallerUrl { get; set; } = string.Empty;

    public string InstallerUrl32 { get; set; } = string.Empty;

    public string InstallerUrl64 { get; set; } = string.Empty;

    public string InstallerUrlArm64 { get; set; } = string.Empty;

    // Optional explicit output file name for downloaded installers.
    public string InstallerFileName { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public string Sha25632 { get; set; } = string.Empty;

    public string Sha25664 { get; set; } = string.Empty;

    public string Command { get; set; } = string.Empty;

    public string SilentCommand { get; set; } = string.Empty;

    // Optional arguments when running a downloaded installer file.
    public string Arguments { get; set; } = string.Empty;

    public string SilentArguments { get; set; } = string.Empty;

    public string SilentArgumentsArm64 { get; set; } = string.Empty;

    public string Architecture { get; set; } = string.Empty;

    public bool HasArm64Support { get; set; }

    public string PortableArchiveUrl { get; set; } = string.Empty;

    public string PortableExecutable { get; set; } = string.Empty;

    public string PortableArchiveType { get; set; } = "zip";

    public string PortableSubfolder { get; set; } = string.Empty;

    public string VirusTotalUrl { get; set; } = string.Empty;

    public string VirusTotalRatio { get; set; } = string.Empty;

    public DateTime VirusTotalScanDate { get; set; } = DateTime.MinValue;

    /// <summary>Optional PowerShell script or .ps1 path to run before installation begins.</summary>
    public string PreInstallScript { get; set; } = string.Empty;

    /// <summary>Optional PowerShell script or .ps1 path to run after installation completes.</summary>
    public string PostInstallScript { get; set; } = string.Empty;

    public bool RequiresRestart { get; set; }

    public bool RequiresElevation { get; set; }

    public bool NeedsManualInstall { get; set; }

    public int VerificationTimeoutSeconds { get; set; } = 35;

    public string DetectDisplayNameContains { get; set; } = string.Empty;

    public string DetectExecutable { get; set; } = string.Empty;
}
