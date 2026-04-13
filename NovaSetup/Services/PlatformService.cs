using System.Diagnostics;
using System.Runtime.InteropServices;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class PlatformService
{
    private static readonly object PackageManagerCacheLock = new();
    private static string? s_cachedPackageManager;

    public enum PlatformKind
    {
        Unknown,
        Windows,
        Linux,
        MacOS
    }

    public sealed class PlatformInfo
    {
        public PlatformKind Kind { get; init; } = PlatformKind.Unknown;

        public string Id { get; init; } = Unknown;

        public string Label { get; init; } = "Unknown OS";

        public string Icon { get; init; } = "OS";
    }

    public const string Windows = "windows";
    public const string Linux = "linux";
    public const string MacOS = "macos";
    public const string Unknown = "unknown";

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

    public static bool IsWindows()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    public static bool IsLinux()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    }

    public static bool IsMacOS()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    }

    public static string GetCurrentPlatform()
    {
        if (IsWindows())
        {
            return Windows;
        }

        if (IsLinux())
        {
            return Linux;
        }

        if (IsMacOS())
        {
            return MacOS;
        }

        return Unknown;
    }

    public static string GetPackageManager()
    {
        if (!string.IsNullOrWhiteSpace(s_cachedPackageManager))
        {
            return s_cachedPackageManager;
        }

        lock (PackageManagerCacheLock)
        {
            if (!string.IsNullOrWhiteSpace(s_cachedPackageManager))
            {
                return s_cachedPackageManager;
            }

            s_cachedPackageManager = DetectPackageManager();
            return s_cachedPackageManager;
        }
    }

    private static string DetectPackageManager()
    {
        if (IsWindows())
        {
            if (CommandExists("where.exe", "winget"))
            {
                return "winget";
            }

            if (CommandExists("where.exe", "choco"))
            {
                return "choco";
            }

            return "direct";
        }

        if (IsLinux())
        {
            if (CommandExists("which", "apt"))
            {
                return "apt";
            }

            if (CommandExists("which", "snap"))
            {
                return "snap";
            }

            if (CommandExists("which", "flatpak"))
            {
                return "flatpak";
            }

            return "direct";
        }

        if (IsMacOS())
        {
            return CommandExists("which", "brew") ? "brew" : "direct";
        }

        return "direct";
    }

    public PlatformInfo GetCurrentPlatformInfo()
    {
        if (IsWindows())
        {
            return new PlatformInfo
            {
                Kind = PlatformKind.Windows,
                Id = Windows,
                Label = "Windows",
                Icon = "WIN"
            };
        }

        if (IsLinux())
        {
            return new PlatformInfo
            {
                Kind = PlatformKind.Linux,
                Id = Linux,
                Label = "Linux",
                Icon = "LNX"
            };
        }

        if (IsMacOS())
        {
            return new PlatformInfo
            {
                Kind = PlatformKind.MacOS,
                Id = MacOS,
                Label = "macOS",
                Icon = "MAC"
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
            MacOS => "macOS",
            _ => "Unknown OS"
        };
    }

    public string GetPlatformIcon(string platform)
    {
        return platform switch
        {
            Windows => "WIN",
            Linux => "LNX",
            MacOS => "MAC",
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
            MacOS => support.MacOS,
            _ => false
        };
    }

    private static bool CommandExists(string fileName, string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            if (!process.WaitForExit(3000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best-effort cleanup.
                }

                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
