namespace NovaSetup.Models;

public sealed class AppSettings : ObservableObject
{
    private bool _silentInstallEnabled = true;
    private bool _selfDeleteEnabled;
    private bool _showUnsupportedApps = true;
    private string _defaultInstallLocation = string.Empty;
    private string _profileName = "Default";

    public bool SilentInstallEnabled
    {
        get => _silentInstallEnabled;
        set => SetProperty(ref _silentInstallEnabled, value);
    }

    public bool SelfDeleteEnabled
    {
        get => _selfDeleteEnabled;
        set => SetProperty(ref _selfDeleteEnabled, value);
    }

    public bool ShowUnsupportedApps
    {
        get => _showUnsupportedApps;
        set => SetProperty(ref _showUnsupportedApps, value);
    }

    public string DefaultInstallLocation
    {
        get => _defaultInstallLocation;
        set => SetProperty(ref _defaultInstallLocation, value ?? string.Empty);
    }

    // Placeholder for future profile save/load workflow.
    public string ProfileName
    {
        get => _profileName;
        set => SetProperty(ref _profileName, string.IsNullOrWhiteSpace(value) ? "Default" : value.Trim());
    }
}
