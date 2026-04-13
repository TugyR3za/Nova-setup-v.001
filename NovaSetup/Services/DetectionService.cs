using System.Diagnostics;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class DetectionService
{
    private static readonly string[] KnownGpuVendors = { "NVIDIA", "AMD", "Intel" };
    private static readonly string[] KnownAccessoryVendors = { "Logitech", "Razer", "Corsair", "SteelSeries" };
    private static readonly Regex WingetListSplitRegex = new(@"\s{2,}", RegexOptions.Compiled);
    private static readonly Regex AnsiEscapeRegex = new(@"\x1B\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);
    private static readonly TimeSpan DetectionCacheValidity = TimeSpan.FromMinutes(30);
    private const string ChromeClientRegistryKey = @"SOFTWARE\Google\Update\Clients\{8A69D345-D564-463C-AFF1-A69D9E530F96}";

    private readonly LoggingService? _loggingService;
    private readonly PortableAppService _portableAppService;

    public Func<AppSettings?>? SettingsAccessor { get; set; }

    public DetectionService(LoggingService? loggingService = null)
    {
        _loggingService = loggingService;
        _portableAppService = new PortableAppService(loggingService);
    }

    public string? DetectGpuVendor(string currentPlatform)
    {
        var output = currentPlatform == PlatformService.Windows
            ? RunPowerShellCommand("(Get-CimInstance Win32_VideoController).Name")
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
            ? RunPowerShellCommand("(Get-CimInstance Win32_BaseBoard).Manufacturer")
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
            ? RunPowerShellCommand("(Get-CimInstance Win32_PnPEntity).Name -join \"`n\"")
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

    /// <summary>
    /// Runs all hardware detection queries in a single PowerShell process for speed.
    /// Like Chocolatey, we use Get-CimInstance instead of deprecated wmic.
    /// </summary>
    public HardwareDetectionResult DetectHardware(string currentPlatform)
    {
        if (currentPlatform == PlatformService.Windows)
        {
            return DetectHardwareBatchedPowerShell();
        }

        // Linux fallback — parallel subprocess calls
        string? gpuVendor = null;
        string? motherboardVendor = null;
        IReadOnlyList<string> accessoryVendors = Array.Empty<string>();

        var gpuTask = Task.Run(() => gpuVendor = DetectGpuVendor(currentPlatform));
        var boardTask = Task.Run(() => motherboardVendor = DetectMotherboardVendor(currentPlatform));
        var accessoryTask = Task.Run(() => accessoryVendors = DetectAccessoryVendors(currentPlatform));

        Task.WaitAll(gpuTask, boardTask, accessoryTask);

        return new HardwareDetectionResult
        {
            GpuVendor = gpuVendor,
            MotherboardVendor = motherboardVendor,
            AccessoryVendors = accessoryVendors.ToList()
        };
    }

    /// <summary>
    /// Single PowerShell process that runs GPU + Motherboard + Accessory detection
    /// all at once — replaces 3 separate wmic calls with one fast CIM batch.
    /// </summary>
    private HardwareDetectionResult DetectHardwareBatchedPowerShell()
    {
        const string batchScript =
            "$gpu = (Get-CimInstance Win32_VideoController -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name) -join '|'; " +
            "$mb = (Get-CimInstance Win32_BaseBoard -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Manufacturer) -join '|'; " +
            "$pnp = (Get-CimInstance Win32_PnPEntity -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name) -join '|'; " +
            "Write-Output \"GPU:$gpu\"; " +
            "Write-Output \"MB:$mb\"; " +
            "Write-Output \"PNP:$pnp\"";

        var output = RunPowerShellCommand(batchScript, timeoutMs: 8000);
        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        string gpuLine = string.Empty, mbLine = string.Empty, pnpLine = string.Empty;
        foreach (var line in lines)
        {
            if (line.StartsWith("GPU:", StringComparison.OrdinalIgnoreCase))
                gpuLine = line[4..];
            else if (line.StartsWith("MB:", StringComparison.OrdinalIgnoreCase))
                mbLine = line[3..];
            else if (line.StartsWith("PNP:", StringComparison.OrdinalIgnoreCase))
                pnpLine = line[4..];
        }

        var gpuVendor = ExtractKnownVendor(gpuLine, KnownGpuVendors);
        if (!string.IsNullOrWhiteSpace(gpuVendor))
            _loggingService?.LogInfo($"Detected GPU vendor: {gpuVendor}");

        var motherboardVendor = ExtractCommonBoardVendor(mbLine);
        if (!string.IsNullOrWhiteSpace(motherboardVendor))
            _loggingService?.LogInfo($"Detected motherboard vendor: {motherboardVendor}");

        var accessoryVendors = new List<string>();
        foreach (var vendor in KnownAccessoryVendors)
        {
            if (pnpLine.Contains(vendor, StringComparison.OrdinalIgnoreCase))
                accessoryVendors.Add(vendor);
        }
        if (accessoryVendors.Count > 0)
            _loggingService?.LogInfo($"Detected accessory vendors: {string.Join(", ", accessoryVendors)}");

        return new HardwareDetectionResult
        {
            GpuVendor = gpuVendor,
            MotherboardVendor = motherboardVendor,
            AccessoryVendors = accessoryVendors
        };
    }

    public int DetectInstalledApps(IList<AppItem> apps, string currentPlatform)
    {
        var detectedStates = DetectInstalledAppStates(apps, currentPlatform);

        foreach (var app in apps)
        {
            if (detectedStates.TryGetValue(app.Id, out var state))
            {
                app.IsInstalled = state.IsInstalled;
                app.InstalledVersion = state.InstalledVersion;
            }
            else
            {
                app.IsInstalled = false;
                app.InstalledVersion = string.Empty;
            }
        }

        var detectedCount = detectedStates.Count;
        _loggingService?.LogInfo($"Installed app detection completed. DetectedInstalledApps={detectedCount}");
        return detectedCount;
    }

    public Task<int> DetectInstalledAppsAsync(IList<AppItem> apps)
    {
        var currentPlatform = PlatformService.GetCurrentPlatform();
        return Task.Run(() => DetectInstalledApps(apps, currentPlatform));
    }

    public void InvalidateDetectionCache()
    {
        try
        {
            var cachePath = GetDetectionCachePath();
            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
            }
        }
        catch
        {
            // Best-effort cache invalidation only.
        }
    }

    public IReadOnlyDictionary<string, InstalledAppState> DetectInstalledAppStates(IEnumerable<AppItem> apps, string currentPlatform)
    {
        var appList = apps?.ToList() ?? new List<AppItem>();
        if (TryLoadDetectionCache(appList, currentPlatform, out var cachedStates))
        {
            _loggingService?.LogInfo(
                $"Loaded installed app detection from cache. Platform={currentPlatform}, DetectedInstalledApps={cachedStates.Count}");
            return cachedStates;
        }

        IReadOnlyDictionary<string, InstalledAppState> detectedStates = currentPlatform switch
        {
            PlatformService.Windows => DetectWindowsInstalledAppStates(appList),
            PlatformService.MacOS => DetectMacOSInstalledAppStates(appList),
            _ => DetectCommandBasedInstalledAppStates(appList, currentPlatform)
        };

        TryWriteDetectionCache(currentPlatform, detectedStates);

        var detectedCount = detectedStates.Count;
        _loggingService?.LogInfo(
            $"Installed app detection completed. Platform={currentPlatform}, DetectedInstalledApps={detectedCount}");
        return detectedStates;
    }

    public IReadOnlyList<string> DetectInstalledAppIds(IEnumerable<AppItem> apps, string currentPlatform)
    {
        return DetectInstalledAppStates(apps, currentPlatform).Keys.ToList();
    }

    public bool IsAppInstalled(AppItem app, string currentPlatform)
    {
        if (app is null)
        {
            return false;
        }

        if (currentPlatform == PlatformService.Windows)
        {
            if (app.IsPortable)
            {
                var portableMatch = DetectPortableWindowsApp(app);
                app.InstalledVersion = portableMatch?.InstalledVersion ?? string.Empty;
                return portableMatch is not null;
            }

            var match = DetectWindowsApp(app, BuildWindowsInstallIndex(new[] { app }));
            app.InstalledVersion = match?.InstalledVersion ?? string.Empty;
            return match is not null;
        }

        if (currentPlatform == PlatformService.MacOS)
        {
            var isInstalledOnMacOS = DetectMacOSApp(app);
            app.InstalledVersion = string.Empty;
            return isInstalledOnMacOS;
        }

        var isInstalled = IsCommandBasedAppInstalled(app, currentPlatform);
        if (!isInstalled)
        {
            app.InstalledVersion = string.Empty;
        }

        return isInstalled;
    }

    public string? TryGetInstalledExecutablePath(AppItem app, string currentPlatform)
    {
        if (app is null)
        {
            return null;
        }

        if (currentPlatform == PlatformService.Windows)
        {
            if (app.IsPortable)
            {
                var portableRoot = ResolvePortableInstallRoot(app);
                if (string.IsNullOrWhiteSpace(portableRoot))
                {
                    return null;
                }

                var portableExecutable = _portableAppService.FindPortableExe(app, portableRoot);
                if (!string.IsNullOrWhiteSpace(portableExecutable))
                {
                    app.PortableInstallPath = portableRoot;
                }

                return portableExecutable;
            }

            var detectionIndex = BuildWindowsInstallIndex(new[] { app });
            var explicitDisplayNameMatch = FindExplicitDisplayNameMatch(app, detectionIndex.UninstallEntries);
            var explicitMatchPath = TryResolveExecutablePathFromEntry(app, explicitDisplayNameMatch);
            if (!string.IsNullOrWhiteSpace(explicitMatchPath))
            {
                return explicitMatchPath;
            }

            var uninstallMatch = FindRegisteredUninstallEntry(app, detectionIndex.UninstallEntries);
            var uninstallMatchPath = TryResolveExecutablePathFromEntry(app, uninstallMatch);
            if (!string.IsNullOrWhiteSpace(uninstallMatchPath))
            {
                return uninstallMatchPath;
            }

            if (TryGetRegisteredAppPath(app, detectionIndex.AppPathExecutables, out var registeredPath))
            {
                return registeredPath;
            }

            var executableOnPath = TryGetExecutableOnPath(app, currentPlatform);
            if (!string.IsNullOrWhiteSpace(executableOnPath))
            {
                return executableOnPath;
            }

            return TryGetExecutableInKnownWindowsLocations(app);
        }

        if (currentPlatform == PlatformService.MacOS)
        {
            var appBundlePath = TryGetMacOSAppBundlePath(app);
            if (!string.IsNullOrWhiteSpace(appBundlePath))
            {
                return appBundlePath;
            }
        }

        return TryGetExecutableOnPath(app, currentPlatform);
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

            var autoSelected = false;
            if (supportedOnCurrentPlatform && autoSelectSupportedApps && !matchedApp.IsSelected)
            {
                matchedApp.IsSelected = true;
                autoSelected = true;
            }

            matchedApp.RecommendationReason = supportedOnCurrentPlatform
                ? autoSelected
                    ? $"{candidate.Reason} Recommended and auto-selected."
                    : $"{candidate.Reason} Recommended for this system."
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
                $"Recommended app '{matchedApp.Name}' for {candidate.DisplayName}. Supported={supportedOnCurrentPlatform}, AutoSelected={autoSelected}");
        }

        _loggingService?.LogInfo(
            $"Recommendation summary: Total={summary.RecommendedAppIds.Count}, Supported={summary.SupportedRecommendations}, Unsupported={summary.UnsupportedRecommendations}");

        return summary;
    }

    private IReadOnlyDictionary<string, InstalledAppState> DetectWindowsInstalledAppStates(IReadOnlyList<AppItem> apps)
    {
        var detectionIndex = BuildWindowsInstallIndex(apps);
        var wingetVersions = TryBatchGetWingetInstalledVersions(apps);
        var detectedStates = new Dictionary<string, InstalledAppState>(StringComparer.OrdinalIgnoreCase);

        foreach (var app in apps)
        {
            if (app.IsPortable)
            {
                var portableMatch = DetectPortableWindowsApp(app);
                if (portableMatch is null)
                {
                    continue;
                }

                detectedStates[app.Id] = new InstalledAppState(true, portableMatch.InstalledVersion);
                var portableVersionSuffix = string.IsNullOrWhiteSpace(portableMatch.InstalledVersion)
                    ? string.Empty
                    : $" Version={portableMatch.InstalledVersion}";
                _loggingService?.LogInfo($"Detected installed app '{app.Name}' via {portableMatch.Source}.{portableVersionSuffix}");
                continue;
            }

            var detectionMatch = DetectWindowsApp(app, detectionIndex, wingetVersions);
            if (detectionMatch is null)
            {
                continue;
            }

            detectedStates[app.Id] = new InstalledAppState(true, detectionMatch.InstalledVersion);
            var versionSuffix = string.IsNullOrWhiteSpace(detectionMatch.InstalledVersion)
                ? string.Empty
                : $" Version={detectionMatch.InstalledVersion}";
            _loggingService?.LogInfo($"Detected installed app '{app.Name}' via {detectionMatch.Source}.{versionSuffix}");
        }

        _loggingService?.LogInfo(
            $"Windows install index prepared. RegistryEntries={detectionIndex.UninstallEntries.Count}, AppPathMatches={detectionIndex.AppPathExecutables.Count}");

        return detectedStates;
    }

    private IReadOnlyDictionary<string, InstalledAppState> DetectCommandBasedInstalledAppStates(IReadOnlyList<AppItem> apps, string currentPlatform)
    {
        var detectedStates = new Dictionary<string, InstalledAppState>(StringComparer.OrdinalIgnoreCase);

        foreach (var app in apps)
        {
            if (!IsCommandBasedAppInstalled(app, currentPlatform))
            {
                continue;
            }

            detectedStates[app.Id] = new InstalledAppState(true, string.Empty);
        }

        return detectedStates;
    }

    private IReadOnlyDictionary<string, InstalledAppState> DetectMacOSInstalledAppStates(IReadOnlyList<AppItem> apps)
    {
        var detectedStates = new Dictionary<string, InstalledAppState>(StringComparer.OrdinalIgnoreCase);
        if (!OperatingSystem.IsMacOS())
        {
            return detectedStates;
        }

        foreach (var app in apps)
        {
            if (!DetectMacOSApp(app))
            {
                continue;
            }

            detectedStates[app.Id] = new InstalledAppState(true, string.Empty);
        }

        return detectedStates;
    }

    private WindowsInstallMatch? DetectWindowsApp(
        AppItem app,
        WindowsInstallIndex detectionIndex,
        IReadOnlyDictionary<string, string>? wingetVersions = null)
    {
        var explicitDisplayNameMatch = FindExplicitDisplayNameMatch(app, detectionIndex.UninstallEntries);
        if (explicitDisplayNameMatch is not null)
        {
            return new WindowsInstallMatch(
                "Windows registry",
                ResolveDetectedVersion(app, explicitDisplayNameMatch, wingetVersions: wingetVersions));
        }

        var uninstallMatch = FindRegisteredUninstallEntry(app, detectionIndex.UninstallEntries);
        if (uninstallMatch is not null)
        {
            return new WindowsInstallMatch(
                "Windows registry",
                ResolveDetectedVersion(app, uninstallMatch, wingetVersions: wingetVersions));
        }

        if (TryGetRegisteredAppPath(app, detectionIndex.AppPathExecutables, out var registeredAppPath))
        {
            return new WindowsInstallMatch(
                "App Paths",
                ResolveDetectedVersion(app, executablePath: registeredAppPath, wingetVersions: wingetVersions));
        }

        var executableOnPath = TryGetExecutableOnPath(app, PlatformService.Windows);
        if (!string.IsNullOrWhiteSpace(executableOnPath))
        {
            return new WindowsInstallMatch(
                "PATH",
                ResolveDetectedVersion(app, executablePath: executableOnPath, wingetVersions: wingetVersions));
        }

        var executableInKnownLocation = TryGetExecutableInKnownWindowsLocations(app);
        if (!string.IsNullOrWhiteSpace(executableInKnownLocation))
        {
            return new WindowsInstallMatch(
                "Known install path",
                ResolveDetectedVersion(app, executablePath: executableInKnownLocation, wingetVersions: wingetVersions));
        }

        return null;
    }

    private WindowsInstallMatch? DetectPortableWindowsApp(AppItem app)
    {
        if (!OperatingSystem.IsWindows() || !app.IsPortable)
        {
            return null;
        }

        var portableRoot = ResolvePortableInstallRoot(app);
        if (string.IsNullOrWhiteSpace(portableRoot))
        {
            return null;
        }

        var executablePath = _portableAppService.FindPortableExe(app, portableRoot);
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        app.PortableInstallPath = portableRoot;
        return new WindowsInstallMatch(
            "Portable folder",
            TryGetFileVersion(executablePath));
    }

    private static WindowsInstalledEntry? FindExplicitDisplayNameMatch(AppItem app, IEnumerable<WindowsInstalledEntry> uninstallEntries)
    {
        var explicitDisplayName = app.WindowsInstall?.DetectDisplayNameContains;
        if (string.IsNullOrWhiteSpace(explicitDisplayName))
        {
            return null;
        }

        return uninstallEntries.FirstOrDefault(entry =>
            !string.IsNullOrWhiteSpace(entry.DisplayName) &&
            entry.DisplayName.Contains(explicitDisplayName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSupportedOnPlatform(AppItem app, string currentPlatform)
    {
        return currentPlatform switch
        {
            PlatformService.Windows => app.SupportedPlatforms.Windows,
            PlatformService.Linux => app.SupportedPlatforms.Linux,
            PlatformService.MacOS => app.SupportedPlatforms.MacOS,
            _ => false
        };
    }

    private static bool IsCommandBasedAppInstalled(AppItem app, string currentPlatform)
    {
        return HasExecutableOnPath(app, currentPlatform);
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

    private static int RunCommandExitCode(string fileName, string arguments, int timeoutMs = 3000)
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
            if (!process.WaitForExit(timeoutMs))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best-effort timeout cleanup.
                }

                return -1;
            }

            return process.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Runs a PowerShell command and returns stdout. Uses -NoProfile for faster startup.
    /// This is the Chocolatey-style approach: PowerShell is faster than wmic/where.exe.
    /// </summary>
    private static string RunPowerShellCommand(string command, int timeoutMs = 5000)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NoLogo -NonInteractive -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return string.Empty;
            }

            return output;
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

    private static string EscapeSingleQuotes(string value)
    {
        return value.Replace("'", "'\"'\"'", StringComparison.Ordinal);
    }

    private static bool HasExecutableOnPath(AppItem app, string currentPlatform)
    {
        return !string.IsNullOrWhiteSpace(TryGetExecutableOnPath(app, currentPlatform));
    }

    private static bool DetectMacOSApp(AppItem app)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        var appBundlePath = TryGetMacOSAppBundlePath(app);
        if (!string.IsNullOrWhiteSpace(appBundlePath))
        {
            return true;
        }

        var brewPackageId = ResolveMacOSBrewPackageId(app);
        if (!string.IsNullOrWhiteSpace(brewPackageId) &&
            RunCommandExitCode("brew", $"list {brewPackageId}") == 0)
        {
            return true;
        }

        return HasExecutableOnPath(app, PlatformService.MacOS);
    }

    private static string? TryGetMacOSAppBundlePath(AppItem app)
    {
        if (!OperatingSystem.IsMacOS() || string.IsNullOrWhiteSpace(app?.Name))
        {
            return null;
        }

        var bundlePath = Path.Combine("/Applications", $"{app.Name}.app");
        return Directory.Exists(bundlePath) ? bundlePath : null;
    }

    private static string ResolveMacOSBrewPackageId(AppItem app)
    {
        var command = app.MacOSInstall?.Command ?? string.Empty;
        if (string.IsNullOrWhiteSpace(command))
        {
            return app.Id;
        }

        var tokens = command.Split(
            [' ', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var installIndex = Array.FindIndex(tokens, token => string.Equals(token, "install", StringComparison.OrdinalIgnoreCase));
        if (installIndex < 0)
        {
            return app.Id;
        }

        for (var index = installIndex + 1; index < tokens.Length; index++)
        {
            var token = tokens[index];
            if (token.StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            return token;
        }

        return app.Id;
    }

    private static string? TryGetExecutableOnPath(AppItem app, string currentPlatform)
    {
        var detectExecutable = currentPlatform switch
        {
            PlatformService.Windows => app.WindowsInstall?.DetectExecutable,
            PlatformService.Linux => app.LinuxInstall?.DetectExecutable,
            PlatformService.MacOS => app.MacOSInstall?.DetectExecutable,
            _ => string.Empty
        };

        foreach (var candidate in BuildExecutableCandidates(detectExecutable))
        {
            var output = currentPlatform == PlatformService.Windows
                ? RunPowerShellCommand($"(Get-Command '{candidate}' -ErrorAction SilentlyContinue).Source")
                : RunCommand("sh", $"-c \"command -v '{EscapeSingleQuotes(candidate)}'\"");

            if (!string.IsNullOrWhiteSpace(output))
            {
                return output
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault();
            }
        }

        return null;
    }

    private static WindowsInstallIndex BuildWindowsInstallIndex(IEnumerable<AppItem> apps)
    {
        var executables = apps
            .SelectMany(app => BuildExecutableCandidates(app.WindowsInstall?.DetectExecutable))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new WindowsInstallIndex(
            ReadWindowsUninstallEntries(),
            ReadWindowsAppPathExecutables(executables));
    }

    private static List<WindowsInstalledEntry> ReadWindowsUninstallEntries()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new List<WindowsInstalledEntry>();
        }

        var entries = new List<WindowsInstalledEntry>();
        var fingerprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var locations = new[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (RegistryHive.CurrentUser, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (RegistryHive.CurrentUser, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")
        };

        foreach (var (hive, view, subKeyPath) in locations)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var uninstallKey = baseKey.OpenSubKey(subKeyPath);
                if (uninstallKey is null)
                {
                    continue;
                }

                foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                {
                    using var appKey = uninstallKey.OpenSubKey(subKeyName);
                    if (appKey is null)
                    {
                        continue;
                    }

                    var displayName = appKey.GetValue("DisplayName")?.ToString() ?? string.Empty;
                    var publisher = appKey.GetValue("Publisher")?.ToString() ?? string.Empty;
                    var displayVersion = appKey.GetValue("DisplayVersion")?.ToString() ?? string.Empty;
                    var installLocation = appKey.GetValue("InstallLocation")?.ToString() ?? string.Empty;
                    var displayIcon = appKey.GetValue("DisplayIcon")?.ToString() ?? string.Empty;
                    var uninstallString = appKey.GetValue("UninstallString")?.ToString() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(displayName) &&
                        string.IsNullOrWhiteSpace(displayIcon) &&
                        string.IsNullOrWhiteSpace(uninstallString))
                    {
                        continue;
                    }

                    var fingerprint = $"{displayName}|{publisher}|{displayVersion}|{installLocation}|{displayIcon}|{uninstallString}";
                    if (!fingerprints.Add(fingerprint))
                    {
                        continue;
                    }

                    entries.Add(new WindowsInstalledEntry(
                        displayName,
                        publisher,
                        displayVersion,
                        installLocation,
                        displayIcon,
                        uninstallString));
                }
            }
            catch
            {
                // Ignore registry access failures and keep scanning other hives/views.
            }
        }

        return entries;
    }

    private static Dictionary<string, string> ReadWindowsAppPathExecutables(IEnumerable<string> executableCandidates)
    {
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!OperatingSystem.IsWindows())
        {
            return found;
        }

        var appPathsRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
        var locations = new[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32),
            (RegistryHive.CurrentUser, RegistryView.Registry64),
            (RegistryHive.CurrentUser, RegistryView.Registry32)
        };

        foreach (var candidate in executableCandidates)
        {
            foreach (var (hive, view) in locations)
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var rootKey = baseKey.OpenSubKey(appPathsRoot);
                    using var appKey = rootKey?.OpenSubKey(candidate);
                    if (appKey is null)
                    {
                        continue;
                    }

                    var registeredPath = appKey.GetValue(string.Empty)?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(registeredPath))
                    {
                        found[NormalizeExecutableCandidate(candidate)] = registeredPath;
                    }
                }
                catch
                {
                    // Ignore per-key failures.
                }
            }
        }

        return found;
    }

    private static WindowsInstalledEntry? FindRegisteredUninstallEntry(AppItem app, IEnumerable<WindowsInstalledEntry> uninstallEntries)
    {
        var appNameTokens = BuildTokens(app.Name);
        if (appNameTokens.Count == 0)
        {
            return null;
        }

        var appPublisherTokens = BuildTokens(app.PublisherName, ignoreNoise: true);
        var detectExecutableTokens = BuildTokens(Path.GetFileNameWithoutExtension(app.WindowsInstall?.DetectExecutable ?? string.Empty));

        foreach (var entry in uninstallEntries)
        {
            if (IsStrongNameMatch(appNameTokens, appPublisherTokens, entry))
            {
                return entry;
            }

            if (detectExecutableTokens.Count > 0 &&
                detectExecutableTokens.All(token => entry.SearchTokens.Contains(token)) &&
                (appPublisherTokens.Count == 0 || entry.PublisherTokens.Overlaps(appPublisherTokens)))
            {
                return entry;
            }
        }

        return null;
    }

    private static bool IsStrongNameMatch(
        HashSet<string> appNameTokens,
        HashSet<string> appPublisherTokens,
        WindowsInstalledEntry entry)
    {
        if (appNameTokens.Count == 1)
        {
            var token = appNameTokens.First();
            if (!entry.DisplayNameTokens.Contains(token))
            {
                return false;
            }

            if (token.Length >= 4)
            {
                return true;
            }

            return appPublisherTokens.Count == 0 || entry.PublisherTokens.Overlaps(appPublisherTokens);
        }

        if (appNameTokens.All(token => entry.DisplayNameTokens.Contains(token)))
        {
            return true;
        }

        return appPublisherTokens.Count > 0 &&
               entry.PublisherTokens.Overlaps(appPublisherTokens) &&
               appNameTokens.Count(token => entry.DisplayNameTokens.Contains(token)) >= Math.Min(2, appNameTokens.Count);
    }

    private static bool TryGetRegisteredAppPath(AppItem app, IReadOnlyDictionary<string, string> appPathExecutables, out string registeredPath)
    {
        registeredPath = string.Empty;
        foreach (var candidate in BuildExecutableCandidates(app.WindowsInstall?.DetectExecutable))
        {
            if (appPathExecutables.TryGetValue(NormalizeExecutableCandidate(candidate), out var candidatePath))
            {
                registeredPath = candidatePath;
                return true;
            }
        }

        return false;
    }

    private static bool HasExecutableInKnownWindowsLocations(AppItem app)
    {
        return !string.IsNullOrWhiteSpace(TryGetExecutableInKnownWindowsLocations(app));
    }

    private static string? TryGetExecutableInKnownWindowsLocations(AppItem app)
    {
        foreach (var path in GetKnownWindowsInstallProbePaths(app))
        {
            try
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
            catch
            {
                // Ignore bad probe paths and keep checking.
            }
        }

        return null;
    }

    private string ResolvePortableInstallRoot(AppItem app)
    {
        var configuredPortableFolder = SettingsAccessor?.Invoke()?.DefaultPortableFolder ?? string.Empty;
        if (string.IsNullOrWhiteSpace(configuredPortableFolder))
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(app.PortableInstallPath) &&
            Directory.Exists(app.PortableInstallPath))
        {
            return app.PortableInstallPath;
        }

        return Path.Combine(configuredPortableFolder, app.Id);
    }

    private static IEnumerable<string> GetKnownWindowsInstallProbePaths(AppItem app)
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        foreach (var candidate in app.Id switch
                 {
                     "firefox" => new[]
                     {
                         Path.Combine(programFiles, "Mozilla Firefox", "firefox.exe"),
                         Path.Combine(programFilesX86, "Mozilla Firefox", "firefox.exe")
                     },
                     "chrome" => new[]
                     {
                         Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"),
                         Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe"),
                         Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe")
                     },
                     "steam" => new[]
                     {
                         Path.Combine(programFilesX86, "Steam", "steam.exe"),
                         Path.Combine(programFiles, "Steam", "steam.exe")
                     },
                     "vscode" => new[]
                     {
                         Path.Combine(localAppData, "Programs", "Microsoft VS Code", "Code.exe"),
                         Path.Combine(programFiles, "Microsoft VS Code", "Code.exe")
                     },
                     "git" => new[]
                     {
                         Path.Combine(programFiles, "Git", "cmd", "git.exe"),
                         Path.Combine(programFiles, "Git", "bin", "git.exe"),
                         Path.Combine(programFilesX86, "Git", "cmd", "git.exe")
                     },
                     "vlc" => new[]
                     {
                         Path.Combine(programFiles, "VideoLAN", "VLC", "vlc.exe"),
                         Path.Combine(programFilesX86, "VideoLAN", "VLC", "vlc.exe")
                     },
                     "7zip" => new[]
                     {
                         Path.Combine(programFiles, "7-Zip", "7zFM.exe"),
                         Path.Combine(programFiles, "7-Zip", "7z.exe"),
                         Path.Combine(programFilesX86, "7-Zip", "7zFM.exe"),
                         Path.Combine(programFilesX86, "7-Zip", "7z.exe")
                     },
                     "nvidia-app" => new[]
                     {
                         Path.Combine(programFiles, "NVIDIA Corporation", "NVIDIA App", "NVIDIA App.exe"),
                         Path.Combine(programFiles, "NVIDIA Corporation", "NVIDIA App", "NVIDIAApp.exe")
                     },
                     "logitech-ghub" => new[]
                     {
                         Path.Combine(programFiles, "LGHUB", "lghub.exe"),
                         Path.Combine(programFilesX86, "LGHUB", "lghub.exe"),
                         Path.Combine(localAppData, "LGHUB", "lghub.exe"),
                         Path.Combine(programFiles, "Logitech", "G HUB", "lghub.exe"),
                         Path.Combine(programFilesX86, "Logitech", "G HUB", "lghub.exe")
                     },
                     _ => Array.Empty<string>()
                 })
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                yield return candidate;
            }
        }
    }

    private string ResolveDetectedVersion(
        AppItem app,
        WindowsInstalledEntry? entry = null,
        string? executablePath = null,
        IReadOnlyDictionary<string, string>? wingetVersions = null)
    {
        if (string.Equals(app.Id, "steam", StringComparison.OrdinalIgnoreCase))
        {
            return app.Version?.Trim() ?? string.Empty;
        }

        var wingetVersion = wingetVersions is not null
            ? TryGetCachedWingetInstalledVersion(app, wingetVersions) ?? string.Empty
            : TryGetWingetInstalledVersion(app);
        if (!string.IsNullOrWhiteSpace(wingetVersion))
        {
            return wingetVersion;
        }

        if (string.Equals(app.Id, "chrome", StringComparison.OrdinalIgnoreCase))
        {
            var chromeVersion = TryGetChromeInstalledVersion();
            if (!string.IsNullOrWhiteSpace(chromeVersion))
            {
                return chromeVersion;
            }
        }

        if (string.Equals(app.Id, "discord", StringComparison.OrdinalIgnoreCase))
        {
            var discordVersion = TryGetDiscordInstalledVersion();
            if (!string.IsNullOrWhiteSpace(discordVersion))
            {
                return discordVersion;
            }
        }

        if (entry is not null && !string.IsNullOrWhiteSpace(entry.DisplayVersion))
        {
            return entry.DisplayVersion.Trim();
        }

        var probePaths = new List<string>();
        if (entry is not null)
        {
            probePaths.AddRange(GetExecutableProbePathsFromEntry(app, entry));
        }

        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            probePaths.Add(executablePath);
        }

        foreach (var candidatePath in probePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var detectedVersion = TryGetFileVersion(candidatePath);
            if (!string.IsNullOrWhiteSpace(detectedVersion))
            {
                return detectedVersion;
            }
        }

        return string.Empty;
    }

    private static string? TryGetCachedWingetInstalledVersion(
        AppItem app,
        IReadOnlyDictionary<string, string>? wingetVersions)
    {
        if (wingetVersions is null ||
            string.IsNullOrWhiteSpace(app.WingetId) ||
            !wingetVersions.TryGetValue(app.WingetId, out var installedVersion) ||
            string.IsNullOrWhiteSpace(installedVersion))
        {
            return null;
        }

        return installedVersion.Trim();
    }

    private static IEnumerable<string> GetExecutableProbePathsFromEntry(AppItem app, WindowsInstalledEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.DisplayIcon))
        {
            var displayIconPath = entry.DisplayIcon;
            var commaIndex = displayIconPath.IndexOf(',');
            if (commaIndex >= 0)
            {
                displayIconPath = displayIconPath[..commaIndex];
            }

            displayIconPath = displayIconPath.Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(displayIconPath))
            {
                yield return displayIconPath;
            }
        }

        if (!string.IsNullOrWhiteSpace(entry.InstallLocation))
        {
            var detectExecutable = app.WindowsInstall?.DetectExecutable ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(detectExecutable))
            {
                yield return Path.Combine(entry.InstallLocation, detectExecutable);
                yield return Path.Combine(entry.InstallLocation, NormalizeExecutableCandidate(detectExecutable));
            }
        }
    }

    private static string? TryResolveExecutablePathFromEntry(AppItem app, WindowsInstalledEntry? entry)
    {
        if (entry is null)
        {
            return null;
        }

        foreach (var candidatePath in GetExecutableProbePathsFromEntry(app, entry)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }
            catch
            {
                // Ignore invalid probe paths and continue searching.
            }
        }

        return null;
    }

    private static string TryGetFileVersion(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return string.Empty;
        }

        try
        {
            if (!File.Exists(filePath))
            {
                return string.Empty;
            }

            var info = FileVersionInfo.GetVersionInfo(filePath);
            return !string.IsNullOrWhiteSpace(info.ProductVersion)
                ? info.ProductVersion.Trim()
                : info.FileVersion?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string TryGetWingetInstalledVersion(AppItem app)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(app.WingetId))
        {
            return string.Empty;
        }

        var wingetPath = ResolveWingetExecutable();
        if (string.IsNullOrWhiteSpace(wingetPath))
        {
            return string.Empty;
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = wingetPath,
                    Arguments =
                        $"list --id {QuoteArgument(app.WingetId)} --exact --accept-source-agreements --disable-interactivity",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(10000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best-effort timeout cleanup only.
                }

                _loggingService?.LogDebug(
                    $"Winget installed-version lookup timed out for {app.Name} ({app.WingetId}).");
                return string.Empty;
            }

            var output = $"{stdoutTask.GetAwaiter().GetResult()}{Environment.NewLine}{stderrTask.GetAwaiter().GetResult()}";
            var installedVersion = ParseWingetInstalledVersion(output, app.WingetId);
            if (!string.IsNullOrWhiteSpace(installedVersion))
            {
                return installedVersion;
            }
        }
        catch (Exception ex)
        {
            _loggingService?.LogDebug(
                $"Winget installed-version lookup failed for {app.Name} ({app.WingetId}): {ex.Message}");
        }

        return string.Empty;
    }

    private Dictionary<string, string> TryBatchGetWingetInstalledVersions(IReadOnlyList<AppItem> apps)
    {
        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!OperatingSystem.IsWindows())
        {
            return results;
        }

        var wingetIds = apps
            .Where(app => !string.IsNullOrWhiteSpace(app.WingetId))
            .Select(app => app.WingetId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (wingetIds.Count == 0)
        {
            return results;
        }

        var wingetPath = ResolveWingetExecutable();
        if (string.IsNullOrWhiteSpace(wingetPath))
        {
            return results;
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = wingetPath,
                    Arguments = "list --accept-source-agreements --disable-interactivity",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(15000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best-effort timeout cleanup only.
                }

                _loggingService?.LogDebug("Batch winget installed-version lookup timed out after 15 seconds.");
                return results;
            }

            var output = string.Join(
                Environment.NewLine,
                new[]
                {
                    stdoutTask.GetAwaiter().GetResult(),
                    stderrTask.GetAwaiter().GetResult()
                }.Where(text => !string.IsNullOrWhiteSpace(text)));

            results = ParseBatchWingetInstalledVersions(output, wingetIds);
            _loggingService?.LogDebug(
                $"Batch winget installed-version lookup resolved {results.Count} package version(s).");
        }
        catch (Exception ex)
        {
            _loggingService?.LogDebug($"Batch winget installed-version lookup failed: {ex.Message}");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return results;
    }

    private static Dictionary<string, string> ParseBatchWingetInstalledVersions(
        string output,
        IReadOnlySet<string> wingetIds)
    {
        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(output) || wingetIds.Count == 0)
        {
            return results;
        }

        var sanitizedOutput = AnsiEscapeRegex.Replace(output, string.Empty);
        var lines = sanitizedOutput.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            if (IsWingetNoiseLine(line))
            {
                continue;
            }

            var columns = WingetListSplitRegex
                .Split(line)
                .Where(column => !string.IsNullOrWhiteSpace(column))
                .ToArray();
            if (columns.Length < 3)
            {
                continue;
            }

            for (var index = 0; index < columns.Length - 1; index++)
            {
                var candidateId = columns[index].Trim();
                if (!wingetIds.Contains(candidateId))
                {
                    continue;
                }

                var candidateVersion = columns[index + 1].Trim();
                if (!string.IsNullOrWhiteSpace(candidateVersion))
                {
                    results[candidateId] = candidateVersion;
                }

                break;
            }
        }

        return results;
    }

    private static bool IsWingetNoiseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return true;
        }

        var trimmed = line.Trim();
        return trimmed.StartsWith("Name", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("No installed package", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("package(s)", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("upgrades available", StringComparison.OrdinalIgnoreCase) ||
               trimmed.All(character => character == '-' || char.IsWhiteSpace(character));
    }

    private static string ParseWingetInstalledVersion(string output, string wingetId)
    {
        if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(wingetId))
        {
            return string.Empty;
        }

        var sanitizedOutput = AnsiEscapeRegex.Replace(output, string.Empty);
        var lines = sanitizedOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("Name", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("No installed package", StringComparison.OrdinalIgnoreCase) ||
                line.All(character => character == '-' || char.IsWhiteSpace(character)))
            {
                continue;
            }

            var columns = WingetListSplitRegex
                .Split(line)
                .Where(column => !string.IsNullOrWhiteSpace(column))
                .ToArray();

            var idIndex = Array.FindIndex(
                columns,
                column => string.Equals(column, wingetId, StringComparison.OrdinalIgnoreCase));
            if (idIndex >= 0 && idIndex + 1 < columns.Length)
            {
                return columns[idIndex + 1].Trim();
            }
        }

        return string.Empty;
    }

    private static string ResolveWingetExecutable()
    {
        var localAliasPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "WindowsApps",
            "winget.exe");

        try
        {
            return File.Exists(localAliasPath) ? localAliasPath : "winget.exe";
        }
        catch
        {
            return "winget.exe";
        }
    }

    private static string QuoteArgument(string value)
    {
        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static string TryGetChromeInstalledVersion()
    {
        if (!OperatingSystem.IsWindows())
        {
            return string.Empty;
        }

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var chromeKey = baseKey.OpenSubKey(ChromeClientRegistryKey);
                var version = chromeKey?.GetValue("pv")?.ToString();
                if (!string.IsNullOrWhiteSpace(version))
                {
                    return version.Trim();
                }
            }
            catch
            {
                // Ignore per-view failures and keep checking.
            }
        }

        return string.Empty;
    }

    private bool TryLoadDetectionCache(
        IReadOnlyList<AppItem> apps,
        string currentPlatform,
        out IReadOnlyDictionary<string, InstalledAppState> detectedStates)
    {
        detectedStates = new Dictionary<string, InstalledAppState>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var cachePath = GetDetectionCachePath();
            if (!File.Exists(cachePath))
            {
                return false;
            }

            var json = File.ReadAllText(cachePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var cachePayload = JsonSerializer.Deserialize<DetectionCachePayload>(json);
            if (cachePayload is null ||
                cachePayload.CreatedUtc == DateTime.MinValue ||
                DateTime.UtcNow - cachePayload.CreatedUtc > DetectionCacheValidity ||
                !string.Equals(cachePayload.Platform, currentPlatform, StringComparison.OrdinalIgnoreCase) ||
                cachePayload.States is null)
            {
                return false;
            }

            var appIds = apps
                .Where(app => !string.IsNullOrWhiteSpace(app.Id))
                .Select(app => app.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var filteredStates = new Dictionary<string, InstalledAppState>(StringComparer.OrdinalIgnoreCase);
            foreach (var (appId, state) in cachePayload.States)
            {
                if (!appIds.Contains(appId))
                {
                    continue;
                }

                filteredStates[appId] = state;
            }

            detectedStates = filteredStates;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void TryWriteDetectionCache(string currentPlatform, IReadOnlyDictionary<string, InstalledAppState> detectedStates)
    {
        try
        {
            var cachePath = GetDetectionCachePath();
            var cacheDirectory = Path.GetDirectoryName(cachePath);
            if (!string.IsNullOrWhiteSpace(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }

            var payload = new DetectionCachePayload
            {
                Platform = currentPlatform,
                CreatedUtc = DateTime.UtcNow,
                States = detectedStates.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase)
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = false
            });
            File.WriteAllText(cachePath, json);
        }
        catch
        {
            // Best-effort cache persistence only.
        }
    }

    private static string GetDetectionCachePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NovaSetup",
            "detection_cache.json");
    }

    private static string TryGetDiscordInstalledVersion()
    {
        try
        {
            var discordRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Discord");

            if (!Directory.Exists(discordRoot))
            {
                return string.Empty;
            }

            Version? highestVersion = null;
            var highestVersionText = string.Empty;

            foreach (var directory in Directory.EnumerateDirectories(discordRoot, "app-*"))
            {
                var folderName = Path.GetFileName(directory);
                if (string.IsNullOrWhiteSpace(folderName) || folderName.Length <= 4)
                {
                    continue;
                }

                var versionText = folderName[4..];
                if (!Version.TryParse(versionText, out var parsedVersion) || parsedVersion is null)
                {
                    continue;
                }

                if (highestVersion is null || parsedVersion > highestVersion)
                {
                    highestVersion = parsedVersion;
                    highestVersionText = versionText;
                }
            }

            return highestVersionText;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static IEnumerable<string> BuildExecutableCandidates(string? detectExecutable)
    {
        if (string.IsNullOrWhiteSpace(detectExecutable))
        {
            yield break;
        }

        var trimmed = detectExecutable.Trim();
        yield return trimmed;

        var normalized = NormalizeExecutableCandidate(trimmed);
        if (!string.Equals(trimmed, normalized, StringComparison.OrdinalIgnoreCase))
        {
            yield return normalized;
        }
    }

    private static string NormalizeExecutableCandidate(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable))
        {
            return string.Empty;
        }

        return executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? executable
            : executable + ".exe";
    }

    private static HashSet<string> BuildTokens(string? value, bool ignoreNoise = false)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
        {
            return tokens;
        }

        Span<char> buffer = stackalloc char[value.Length];
        var index = 0;
        foreach (var character in value)
        {
            buffer[index++] = char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ';
        }

        var normalized = new string(buffer[..index]);
        foreach (var token in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (ignoreNoise && IgnoredPublisherTokens.Contains(token))
            {
                continue;
            }

            tokens.Add(token);
        }

        return tokens;
    }

    private static readonly HashSet<string> IgnoredPublisherTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "inc",
        "incorporated",
        "corp",
        "corporation",
        "co",
        "company",
        "llc",
        "ltd",
        "limited",
        "gmbh",
        "srl",
        "software",
        "technologies",
        "technology"
    };

    private sealed record RecommendationCandidate(string VendorKey, string DisplayName, string Reason);

    private sealed record WindowsInstallIndex(
        IReadOnlyList<WindowsInstalledEntry> UninstallEntries,
        Dictionary<string, string> AppPathExecutables);

    private sealed record WindowsInstallMatch(string Source, string InstalledVersion);

    private sealed class WindowsInstalledEntry
    {
        public WindowsInstalledEntry(
            string displayName,
            string publisher,
            string displayVersion,
            string installLocation,
            string displayIcon,
            string uninstallString)
        {
            DisplayName = displayName;
            Publisher = publisher;
            DisplayVersion = displayVersion;
            InstallLocation = installLocation;
            DisplayIcon = displayIcon;
            UninstallString = uninstallString;
            DisplayNameTokens = BuildTokens(displayName);
            PublisherTokens = BuildTokens(publisher, ignoreNoise: true);
            SearchTokens = BuildTokens($"{displayName} {publisher} {installLocation} {displayIcon} {uninstallString}", ignoreNoise: true);
        }

        public string DisplayName { get; }

        public string Publisher { get; }

        public string DisplayVersion { get; }

        public string InstallLocation { get; }

        public string DisplayIcon { get; }

        public string UninstallString { get; }

        public HashSet<string> DisplayNameTokens { get; }

        public HashSet<string> PublisherTokens { get; }

        public HashSet<string> SearchTokens { get; }
    }
}

internal sealed class DetectionCachePayload
{
    public string Platform { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.MinValue;

    public Dictionary<string, InstalledAppState> States { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record InstalledAppState(bool IsInstalled, string InstalledVersion);

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
