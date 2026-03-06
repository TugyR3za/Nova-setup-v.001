using System.Diagnostics;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class DetectionService
{
    private static readonly string[] KnownGpuVendors = { "NVIDIA", "AMD", "Intel" };
    private static readonly string[] KnownAccessoryVendors = { "Logitech", "Razer", "Corsair", "SteelSeries" };

    private readonly LoggingService? _loggingService;

    public DetectionService(LoggingService? loggingService = null)
    {
        _loggingService = loggingService;
    }

    public string? DetectGpuVendor(string currentPlatform)
    {
        var output = currentPlatform == PlatformService.Windows
            ? RunCommand("wmic", "path win32_VideoController get Name")
            : RunCommand("sh", "-c \"lspci\"");

        var vendor = ExtractKnownVendor(output, KnownGpuVendors);
        if (!string.IsNullOrWhiteSpace(vendor))
        {
            _loggingService?.LogInfo($"Detected GPU vendor: {vendor}");
        }

        return vendor;
    }

    public string? DetectMotherboardVendor(string currentPlatform)
    {
        var output = currentPlatform == PlatformService.Windows
            ? RunCommand("wmic", "baseboard get Manufacturer")
            : TryReadFile("/sys/devices/virtual/dmi/id/board_vendor");

        var vendor = ExtractCommonBoardVendor(output);
        if (!string.IsNullOrWhiteSpace(vendor))
        {
            _loggingService?.LogInfo($"Detected motherboard vendor: {vendor}");
        }

        return vendor;
    }

    public IReadOnlyList<string> DetectAccessoryVendors(string currentPlatform)
    {
        var output = currentPlatform == PlatformService.Windows
            ? RunCommand("wmic", "path Win32_PnPEntity get Name")
            : RunCommand("sh", "-c \"lsusb\"");

        var detected = new List<string>();
        foreach (var vendor in KnownAccessoryVendors)
        {
            if (output.Contains(vendor, StringComparison.OrdinalIgnoreCase))
            {
                detected.Add(vendor);
            }
        }

        if (detected.Count > 0)
        {
            _loggingService?.LogInfo($"Detected accessory vendors: {string.Join(", ", detected)}");
        }

        return detected;
    }

    public HardwareDetectionResult DetectHardware(string currentPlatform)
    {
        return new HardwareDetectionResult
        {
            GpuVendor = DetectGpuVendor(currentPlatform),
            MotherboardVendor = DetectMotherboardVendor(currentPlatform),
            AccessoryVendors = DetectAccessoryVendors(currentPlatform).ToList()
        };
    }

    public RecommendationSummary ApplyRecommendations(
        IList<AppItem> apps,
        string currentPlatform,
        bool autoSelectSupportedApps = true)
    {
        var detectionResult = DetectHardware(currentPlatform);
        return ApplyRecommendations(apps, currentPlatform, detectionResult, autoSelectSupportedApps);
    }

    public RecommendationSummary ApplyRecommendations(
        IList<AppItem> apps,
        string currentPlatform,
        HardwareDetectionResult detectionResult,
        bool autoSelectSupportedApps = true)
    {
        // Reset existing recommendation flags. Any new recommendations are reapplied from current detection.
        foreach (var app in apps)
        {
            app.IsRecommended = false;
            app.RecommendationReason = string.Empty;
        }

        var summary = new RecommendationSummary();
        summary.RecordDetected(detectionResult);

        var recommendationCandidates = BuildRecommendationCandidates(detectionResult);
        foreach (var candidate in recommendationCandidates)
        {
            var matchedApp = FindOfficialVendorApp(apps, candidate);
            if (matchedApp is null)
            {
                _loggingService?.LogInfo($"No catalog app found for recommendation rule: {candidate.DisplayName}");
                continue;
            }

            matchedApp.IsRecommended = true;
            var supportedOnCurrentPlatform = IsSupportedOnPlatform(matchedApp, currentPlatform);

            if (supportedOnCurrentPlatform && autoSelectSupportedApps)
            {
                matchedApp.IsSelected = true;
            }

            matchedApp.RecommendationReason = supportedOnCurrentPlatform
                ? $"{candidate.Reason} Recommended and auto-selected."
                : $"{candidate.Reason} Recommended but unsupported on {currentPlatform}.";

            summary.RecommendedAppIds.Add(matchedApp.Id);
            if (supportedOnCurrentPlatform)
            {
                summary.SupportedRecommendations++;
            }
            else
            {
                summary.UnsupportedRecommendations++;
            }

            _loggingService?.LogInfo(
                $"Recommended app '{matchedApp.Name}' for {candidate.DisplayName}. Supported={supportedOnCurrentPlatform}, AutoSelected={matchedApp.IsSelected}");
        }

        _loggingService?.LogInfo(
            $"Recommendation summary: Total={summary.RecommendedAppIds.Count}, Supported={summary.SupportedRecommendations}, Unsupported={summary.UnsupportedRecommendations}");

        return summary;
    }

    private static bool IsSupportedOnPlatform(AppItem app, string currentPlatform)
    {
        return currentPlatform switch
        {
            PlatformService.Windows => app.SupportedPlatforms.Windows,
            PlatformService.Linux => app.SupportedPlatforms.Linux,
            _ => false
        };
    }

    private static List<RecommendationCandidate> BuildRecommendationCandidates(HardwareDetectionResult detectionResult)
    {
        var candidates = new List<RecommendationCandidate>();

        if (!string.IsNullOrWhiteSpace(detectionResult.GpuVendor))
        {
            var gpuVendor = NormalizeVendor(detectionResult.GpuVendor);
            var gpuCandidate = gpuVendor switch
            {
                "NVIDIA" => new RecommendationCandidate(
                    "NVIDIA",
                    "NVIDIA GPU",
                    "NVIDIA GPU detected. NVIDIA software can help with driver and device management."),
                "AMD" => new RecommendationCandidate(
                    "AMD",
                    "AMD GPU",
                    "AMD GPU detected. AMD software is recommended for driver and device support."),
                "Intel" => new RecommendationCandidate(
                    "Intel",
                    "Intel GPU",
                    "Intel GPU detected. Intel support/driver tooling is recommended."),
                _ => null
            };

            if (gpuCandidate is not null)
            {
                candidates.Add(gpuCandidate);
            }
        }

        foreach (var accessoryVendor in detectionResult.AccessoryVendors)
        {
            var normalized = NormalizeVendor(accessoryVendor);
            if (normalized is "LOGITECH" or "RAZER" or "CORSAIR" or "STEELSERIES")
            {
                candidates.Add(new RecommendationCandidate(
                    normalized,
                    $"{normalized} accessory device",
                    $"{normalized} accessory hardware detected. Matching vendor software is recommended."));
            }
        }

        if (!string.IsNullOrWhiteSpace(detectionResult.MotherboardVendor))
        {
            var boardVendor = NormalizeVendor(detectionResult.MotherboardVendor);
            candidates.Add(new RecommendationCandidate(
                boardVendor,
                $"{boardVendor} motherboard",
                $"{boardVendor} motherboard vendor detected. Vendor support utility may be useful."));
        }

        // Deduplicate by vendor key.
        return candidates
            .GroupBy(candidate => candidate.VendorKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static AppItem? FindOfficialVendorApp(IEnumerable<AppItem> apps, RecommendationCandidate candidate)
    {
        // Only recommend apps already present in apps.json and matching known vendor signatures.
        return apps.FirstOrDefault(app => IsOfficialVendorMatch(app, candidate.VendorKey));
    }

    private static bool IsOfficialVendorMatch(AppItem app, string vendorKey)
    {
        var key = NormalizeVendor(vendorKey);
        var id = app.Id ?? string.Empty;
        var name = app.Name ?? string.Empty;
        var publisher = app.PublisherName ?? string.Empty;

        bool contains(string value) => value.Contains(key, StringComparison.OrdinalIgnoreCase);
        var publisherMatch = contains(publisher);
        var strongNameMatch = contains(id) && contains(name);

        return key switch
        {
            "NVIDIA" => publisherMatch || strongNameMatch,
            "AMD" => publisherMatch || strongNameMatch,
            "INTEL" => publisherMatch || strongNameMatch,
            "LOGITECH" => publisherMatch || strongNameMatch,
            "RAZER" => publisherMatch || strongNameMatch,
            "CORSAIR" => publisherMatch || strongNameMatch,
            "STEELSERIES" => publisherMatch || strongNameMatch,
            "ASUS" => publisherMatch || strongNameMatch,
            "MSI" => publisherMatch || strongNameMatch,
            "GIGABYTE" => publisherMatch || strongNameMatch,
            "ASROCK" => publisherMatch || strongNameMatch,
            "DELL" => publisherMatch || strongNameMatch,
            "HP" => publisherMatch || strongNameMatch,
            "LENOVO" => publisherMatch || strongNameMatch,
            _ => false
        };
    }

    private static string NormalizeVendor(string vendor)
    {
        return vendor.Trim().ToUpperInvariant();
    }

    private static string? ExtractKnownVendor(string output, IEnumerable<string> vendors)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        return vendors.FirstOrDefault(vendor => output.Contains(vendor, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractCommonBoardVendor(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var common = new[] { "ASUS", "MSI", "Gigabyte", "ASRock", "Dell", "HP", "Lenovo" };
        return ExtractKnownVendor(output, common);
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

    private static string TryReadFile(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed record RecommendationCandidate(string VendorKey, string DisplayName, string Reason);
}

public sealed class HardwareDetectionResult
{
    public string? GpuVendor { get; set; }

    public string? MotherboardVendor { get; set; }

    public List<string> AccessoryVendors { get; set; } = new();
}

public sealed class RecommendationSummary
{
    public List<string> DetectedSignals { get; } = new();

    public List<string> RecommendedAppIds { get; } = new();

    public int SupportedRecommendations { get; set; }

    public int UnsupportedRecommendations { get; set; }

    public void RecordDetected(HardwareDetectionResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.GpuVendor))
        {
            DetectedSignals.Add($"GPU:{result.GpuVendor}");
        }

        if (!string.IsNullOrWhiteSpace(result.MotherboardVendor))
        {
            DetectedSignals.Add($"Motherboard:{result.MotherboardVendor}");
        }

        foreach (var accessory in result.AccessoryVendors)
        {
            DetectedSignals.Add($"Accessory:{accessory}");
        }
    }
}
