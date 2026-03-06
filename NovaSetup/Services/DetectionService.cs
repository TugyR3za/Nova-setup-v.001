using System.Diagnostics;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class DetectionService
{
    private static readonly string[] KnownAccessoryVendors =
    {
        "Logitech",
        "Razer",
        "Corsair",
        "SteelSeries"
    };

    private readonly LoggingService _loggingService;

    public DetectionService(LoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    public void RunDetections(IList<AppItem> catalog, string currentPlatform)
    {
        DetectInstalledApps(catalog, currentPlatform);
        var vendors = DetectHardwareAndAccessoryVendors(currentPlatform);
        ApplyRecommendations(catalog, vendors);
    }

    private void DetectInstalledApps(IList<AppItem> catalog, string currentPlatform)
    {
        foreach (var app in catalog)
        {
            var installDefinition = currentPlatform == PlatformService.Windows
                ? app.WindowsInstall
                : app.LinuxInstall;

            if (installDefinition is null || string.IsNullOrWhiteSpace(installDefinition.DetectExecutable))
            {
                continue;
            }

            app.IsInstalled = CommandExists(installDefinition.DetectExecutable, currentPlatform);
            if (app.IsInstalled)
            {
                app.StatusBadge = "Installed";
            }
        }
    }

    private HashSet<string> DetectHardwareAndAccessoryVendors(string currentPlatform)
    {
        var vendors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var gpuOutput = currentPlatform == PlatformService.Windows
            ? RunCommand("wmic", "path win32_VideoController get Name")
            : RunCommand("sh", "-c \"lspci\"");
        TryAddVendor(vendors, gpuOutput);

        var boardOutput = currentPlatform == PlatformService.Windows
            ? RunCommand("wmic", "baseboard get Manufacturer")
            : TryReadFile("/sys/devices/virtual/dmi/id/board_vendor");
        TryAddVendor(vendors, boardOutput);

        var accessoryOutput = currentPlatform == PlatformService.Windows
            ? RunCommand("wmic", "path Win32_PnPEntity get Name")
            : RunCommand("sh", "-c \"lsusb\"");

        foreach (var vendor in KnownAccessoryVendors)
        {
            if (accessoryOutput.Contains(vendor, StringComparison.OrdinalIgnoreCase))
            {
                vendors.Add(vendor);
            }
        }

        if (vendors.Count > 0)
        {
            _loggingService.Info($"Detected vendor hints: {string.Join(", ", vendors)}");
        }
        else
        {
            _loggingService.Info("No vendor hints detected.");
        }

        return vendors;
    }

    private void ApplyRecommendations(IList<AppItem> catalog, HashSet<string> detectedVendors)
    {
        foreach (var app in catalog)
        {
            app.IsRecommended = false;
            app.RecommendationReason = string.Empty;

            if (app.RecommendedVendors.Count == 0)
            {
                continue;
            }

            var matchedVendor = app.RecommendedVendors.FirstOrDefault(vendor => detectedVendors.Contains(vendor));
            if (string.IsNullOrWhiteSpace(matchedVendor))
            {
                continue;
            }

            app.IsRecommended = true;
            app.RecommendationReason = $"Recommended for detected {matchedVendor} hardware/accessories.";

            if (app.IsSupportedOnCurrentPlatform && !app.IsInstalled)
            {
                app.IsSelected = true;
            }
        }
    }

    private static bool CommandExists(string command, string currentPlatform)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var probeOutput = currentPlatform == PlatformService.Windows
            ? RunCommand("where", command)
            : RunCommand("sh", $"-c \"which {command}\"");

        return !string.IsNullOrWhiteSpace(probeOutput);
    }

    private static void TryAddVendor(HashSet<string> vendors, string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        if (output.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
        {
            vendors.Add("NVIDIA");
        }

        if (output.Contains("AMD", StringComparison.OrdinalIgnoreCase))
        {
            vendors.Add("AMD");
        }

        if (output.Contains("Intel", StringComparison.OrdinalIgnoreCase))
        {
            vendors.Add("Intel");
        }
    }

    private static string RunCommand(string fileName, string arguments)
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
                process.Kill(entireProcessTree: true);
                return string.Empty;
            }

            return process.StandardOutput.ReadToEnd();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string TryReadFile(string filePath)
    {
        try
        {
            return File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
