using NovaSetup.Models;
using System.Runtime.InteropServices;

namespace NovaSetup.Services;

public sealed class PlatformService
{
    public enum PlatformKind
    {
        Unknown,
        Windows,
        Linux
    }

    public sealed class PlatformInfo
    {
        public PlatformKind Kind { get; init; } = PlatformKind.Unknown;

        public string Id { get; init; } = Unknown;

        public string Label { get; init; } = "Unknown OS";

        public string Icon { get; init; } = "OS";
    }

    public const string Windows = "Windows";
    public const string Linux = "Linux";
    public const string Unknown = "Unknown";

    public static string GetArchitecture()
    {
        return RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            _ => "x64"
        };
    }

    public static bool IsArm64()
    {
        return string.Equals(GetArchitecture(), "arm64", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsX64()
    {
        return string.Equals(GetArchitecture(), "x64", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetCurrentPlatform()
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

    // Simple UI-friendly platform result.
    public PlatformInfo GetCurrentPlatformInfo()
    {
        if (OperatingSystem.IsWindows())
        {
            return new PlatformInfo
            {
                Kind = PlatformKind.Windows,
                Id = Windows,
                Label = "Windows",
                Icon = "WIN"
            };
        }

        if (OperatingSystem.IsLinux())
        {
            return new PlatformInfo
            {
                Kind = PlatformKind.Linux,
                Id = Linux,
                Label = "Linux",
                Icon = "LNX"
            };
        }

        return new PlatformInfo
        {
            Kind = PlatformKind.Unknown,
            Id = Unknown,
            Label = "Unknown OS",
            Icon = "OS"
        };
    }

    public string DetectCurrentPlatform()
    {
        return GetCurrentPlatformInfo().Id;
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
        if (support is null)
        {
            return false;
        }

        return platform switch
        {
            Windows => support.Windows,
            Linux => support.Linux,
            _ => false
        };
    }
}
