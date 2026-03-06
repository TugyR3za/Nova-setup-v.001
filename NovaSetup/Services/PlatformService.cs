using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class PlatformService
{
    public const string Windows = "Windows";
    public const string Linux = "Linux";
    public const string Unknown = "Unknown";

    public string DetectCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return Windows;
        }

        if (OperatingSystem.IsLinux())
        {
            return Linux;
        }

        return Unknown;
    }

    public string GetPlatformLabel(string platform)
    {
        return platform switch
        {
            Windows => "Windows",
            Linux => "Linux",
            _ => "Unknown OS"
        };
    }

    public string GetPlatformIcon(string platform)
    {
        return platform switch
        {
            Windows => "WIN",
            Linux => "LNX",
            _ => "OS"
        };
    }

    public bool IsSupportedOnPlatform(PlatformSupport support, string platform)
    {
        return platform switch
        {
            Windows => support.Windows,
            Linux => support.Linux,
            _ => false
        };
    }
}
