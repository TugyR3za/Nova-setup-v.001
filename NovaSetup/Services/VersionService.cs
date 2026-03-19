using System.Reflection;

namespace NovaSetup.Services;

public static class VersionService
{
    public static string GetAppVersion()
    {
        try
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version is null)
            {
                return "Unknown";
            }

            var patch = version.Build >= 0 ? version.Build : 0;
            return $"{version.Major}.{version.Minor}.{patch}";
        }
        catch
        {
            return "Unknown";
        }
    }

    public static string GetFullVersionString()
    {
        return $"Nova v{GetAppVersion()}";
    }
}
