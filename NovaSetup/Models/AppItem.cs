using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NovaSetup.Models;

public class AppItem : ObservableObject
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _category = string.Empty;
    private string _publisherName = string.Empty;
    private string _homepageUrl = string.Empty;
    private string _description = string.Empty;
    private string _iconPath = string.Empty;
    private string _version = string.Empty;
    private string _installedVersion = string.Empty;
    private string _license = string.Empty;
    private string _releaseNotesUrl = string.Empty;
    private List<string> _dependencies = new();
    private PlatformSupport _supportedPlatforms = new();
    private InstallDefinition? _windowsInstall;
    private InstallDefinition? _linuxInstall;
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
    private string _statusBadge = "Not Installed";
    private double _rowOpacity = 1.0;

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
        set => SetProperty(ref _homepageUrl, value);
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

    public string Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
    }

    [JsonIgnore]
    public string InstalledVersion
    {
        get => _installedVersion;
        set => SetProperty(ref _installedVersion, value);
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

    public PlatformSupport SupportedPlatforms
    {
        get => _supportedPlatforms;
        set => SetProperty(ref _supportedPlatforms, value);
    }

    public InstallDefinition? WindowsInstall
    {
        get => _windowsInstall;
        set => SetProperty(ref _windowsInstall, value);
    }

    public InstallDefinition? LinuxInstall
    {
        get => _linuxInstall;
        set => SetProperty(ref _linuxInstall, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsInstalled
    {
        get => _isInstalled;
        set => SetProperty(ref _isInstalled, value);
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
    public string IconGlyph => string.IsNullOrWhiteSpace(Name) ? "?" : Name[0].ToString().ToUpperInvariant();

    [JsonIgnore]
    public bool HasUpdateAvailable =>
        IsInstalled &&
        !string.IsNullOrEmpty(Version) &&
        !string.IsNullOrEmpty(InstalledVersion) &&
        Version != InstalledVersion;
}

public class InstallDefinition
{
    // Optional direct installer URL for download-first install flows.
    public string InstallerUrl { get; set; } = string.Empty;

    public string InstallerUrl32 { get; set; } = string.Empty;

    public string InstallerUrl64 { get; set; } = string.Empty;

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

    public string Architecture { get; set; } = string.Empty;

    public bool RequiresRestart { get; set; }

    public bool RequiresElevation { get; set; }

    public bool NeedsManualInstall { get; set; }

    public int VerificationTimeoutSeconds { get; set; } = 35;

    public string DetectDisplayNameContains { get; set; } = string.Empty;

    public string DetectExecutable { get; set; } = string.Empty;
}
