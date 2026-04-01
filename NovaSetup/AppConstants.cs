namespace NovaSetup;

/// <summary>
/// Centralized constants for URLs and configuration values.
/// Change these once to point the entire app at a different repository or server.
/// </summary>
public static class AppConstants
{
    /// <summary>Base raw content URL for the GitHub repository.</summary>
    public const string GitHubRawBaseUrl = "https://raw.githubusercontent.com/TugyR3za/Nova-setup-v.001/main";

    /// <summary>Remote apps.json catalog URL.</summary>
    public const string RemoteCatalogUrl = $"{GitHubRawBaseUrl}/NovaSetup/Configs/apps.json";

    /// <summary>Remote version.json URL for self-update checks.</summary>
    public const string VersionCheckUrl = $"{GitHubRawBaseUrl}/version.json";
}
