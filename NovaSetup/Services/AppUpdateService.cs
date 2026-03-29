using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class AppUpdateService
{
    private static readonly Regex WingetIdRegex =
        new(@"--id\s+(?:""(?<id>[^""]+)""|(?<id>\S+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex VersionLineRegex =
        new(@"^\s*Version\s*:\s*(?<value>.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    private readonly LoggingService? _loggingService;

    public AppUpdateService(LoggingService? loggingService = null)
    {
        _loggingService = loggingService;
    }

    public Dictionary<string, string> ResolveLatestCatalogVersions(IEnumerable<AppItem> allApps, string currentPlatform)
    {
        var resolvedVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!OperatingSystem.IsWindows() ||
            !string.Equals(currentPlatform, PlatformService.Windows, StringComparison.OrdinalIgnoreCase))
        {
            return resolvedVersions;
        }

        var wingetPath = ResolveWingetExecutable();
        if (string.IsNullOrWhiteSpace(wingetPath))
        {
            _loggingService?.LogDebug("[AppUpdateService] winget was not found. Using catalog versions as-is.");
            return resolvedVersions;
        }

        foreach (var app in allApps ?? Enumerable.Empty<AppItem>())
        {
            if (app is null || !app.IsInstalled)
            {
                continue;
            }

            if (app.UserDisabledScanning)
            {
                _loggingService?.LogInfo(
                    $"[UpdateService] Skipping update scan for {app.Name} - disabled by user preference");
                continue;
            }

            var packageId = ExtractWingetPackageId(app.WindowsInstall);
            if (string.IsNullOrWhiteSpace(packageId))
            {
                continue;
            }

            if (!TryResolveLatestWingetVersion(wingetPath, packageId, out var latestVersion))
            {
                continue;
            }

            resolvedVersions[app.Id] = latestVersion;
        }

        if (resolvedVersions.Count > 0)
        {
            _loggingService?.LogInfo(
                $"[AppUpdateService] Refreshed latest package versions for {resolvedVersions.Count} app(s) from Winget.");
        }

        return resolvedVersions;
    }

    public List<AppItem> GetAppsWithUpdates(List<AppItem> allApps)
    {
        var updates = (allApps ?? new List<AppItem>())
            .Where(app => !app.UserDisabledScanning && app.HasUpdateAvailable)
            .OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (updates.Count == 0)
        {
            _loggingService?.LogInfo("[AppUpdateService] All installed apps are up to date");
        }
        else
        {
            _loggingService?.LogInfo($"[AppUpdateService] Found {updates.Count} app(s) with updates available");
        }

        return updates;
    }

    public async Task UpdateAppAsync(AppItem app, InstallerService installerService)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(installerService);

        _loggingService?.LogInfo(
            $"[AppUpdateService] Updating {app.Name} from v{app.InstalledVersion} to v{app.Version}");

        var updateCandidate = BuildUpdateInstallCandidate(app);
        var results = await installerService.InstallSelectedAppsAsync(
            new[] { updateCandidate },
            GetCurrentPlatformId(),
            silentInstallEnabled: updateCandidate.SupportsSilentInstall,
            catalogApps: new[] { updateCandidate },
            allowUpgradeForInstalledApps: true);

        if (results.Any(result => result.Success))
        {
            _loggingService?.LogInfo($"[AppUpdateService] Update complete for {app.Name}");
            return;
        }

        var failureMessage = results.LastOrDefault()?.Message ?? "Unknown update result.";
        _loggingService?.LogWarning($"[AppUpdateService] Update did not complete for {app.Name}: {failureMessage}");
    }

    public async Task UpdateAllAsync(List<AppItem> apps, InstallerService installerService)
    {
        foreach (var app in apps ?? new List<AppItem>())
        {
            await UpdateAppAsync(app, installerService);
        }
    }

    private static string GetCurrentPlatformId()
    {
        if (OperatingSystem.IsWindows())
        {
            return PlatformService.Windows;
        }

        if (OperatingSystem.IsLinux())
        {
            return PlatformService.Linux;
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macos";
        }

        return PlatformService.Unknown;
    }

    private static string? ResolveWingetExecutable()
    {
        var localAliasPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "WindowsApps",
            "winget.exe");

        return File.Exists(localAliasPath) ? localAliasPath : "winget.exe";
    }

    private static string ExtractWingetPackageId(InstallDefinition? installDefinition)
    {
        var command = string.IsNullOrWhiteSpace(installDefinition?.SilentCommand)
            ? installDefinition?.Command ?? string.Empty
            : installDefinition.SilentCommand;

        if (string.IsNullOrWhiteSpace(command) ||
            command.IndexOf("winget", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return string.Empty;
        }

        var match = WingetIdRegex.Match(command);
        return match.Success ? match.Groups["id"].Value.Trim() : string.Empty;
    }

    private bool TryResolveLatestWingetVersion(string wingetPath, string packageId, out string latestVersion)
    {
        latestVersion = string.Empty;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = wingetPath,
                    Arguments =
                        $"show --id {QuoteArgument(packageId)} -e --accept-source-agreements --disable-interactivity",
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

            if (!process.WaitForExit(12000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best-effort timeout cleanup only.
                }

                _loggingService?.LogWarning(
                    $"[AppUpdateService] Timed out while resolving latest version for Winget package {packageId}.");
                return false;
            }

            var output = $"{stdoutTask.GetAwaiter().GetResult()}{Environment.NewLine}{stderrTask.GetAwaiter().GetResult()}";
            var versionMatch = VersionLineRegex.Match(output);
            if (!versionMatch.Success)
            {
                _loggingService?.LogDebug(
                    $"[AppUpdateService] Winget did not return a latest version for package {packageId}.");
                return false;
            }

            latestVersion = versionMatch.Groups["value"].Value.Trim();
            if (string.IsNullOrWhiteSpace(latestVersion))
            {
                return false;
            }

            _loggingService?.LogDebug(
                $"[AppUpdateService] Latest Winget version for {packageId}: {latestVersion}");
            return true;
        }
        catch (Exception ex)
        {
            _loggingService?.LogWarning(
                $"[AppUpdateService] Unable to refresh latest version for Winget package {packageId}: {ex.Message}");
            return false;
        }
    }

    private static string QuoteArgument(string value)
    {
        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static AppItem BuildUpdateInstallCandidate(AppItem source)
    {
        var candidate = new AppItem
        {
            Id = source.Id,
            Name = source.Name,
            Category = source.Category,
            PublisherName = source.PublisherName,
            HomepageUrl = source.HomepageUrl,
            Description = source.Description,
            IconPath = source.IconPath,
            Version = source.Version,
            InstalledVersion = source.InstalledVersion,
            License = source.License,
            ReleaseNotesUrl = source.ReleaseNotesUrl,
            Tags = source.Tags.ToList(),
            Dependencies = source.Dependencies.ToList(),
            IsPortable = source.IsPortable,
            PortableInstallPath = source.PortableInstallPath,
            UserDisabledSilentInstall = source.UserDisabledSilentInstall,
            UserDisabledScanning = source.UserDisabledScanning,
            UserDisabledAutoUpdate = source.UserDisabledAutoUpdate,
            SupportedPlatforms = new PlatformSupport
            {
                Windows = source.SupportedPlatforms.Windows,
                Linux = source.SupportedPlatforms.Linux
            },
            WindowsInstall = CloneInstallDefinition(source.WindowsInstall),
            LinuxInstall = CloneInstallDefinition(source.LinuxInstall),
            IsSupportedOnCurrentPlatform = source.IsSupportedOnCurrentPlatform,
            SupportsSilentInstall = source.SupportsSilentInstall,
            IsInstalled = source.IsInstalled
        };

        if (ShouldPreferCommandForUpdate(candidate.WindowsInstall))
        {
            candidate.WindowsInstall!.InstallerUrl = string.Empty;
            candidate.WindowsInstall.InstallerUrl32 = string.Empty;
            candidate.WindowsInstall.InstallerUrl64 = string.Empty;
            candidate.WindowsInstall.InstallerFileName = string.Empty;
            candidate.WindowsInstall.Sha256 = string.Empty;
            candidate.WindowsInstall.Sha25632 = string.Empty;
            candidate.WindowsInstall.Sha25664 = string.Empty;
        }

        return candidate;
    }

    private static InstallDefinition? CloneInstallDefinition(InstallDefinition? source)
    {
        if (source is null)
        {
            return null;
        }

        return new InstallDefinition
        {
            InstallerUrl = source.InstallerUrl,
            InstallerUrl32 = source.InstallerUrl32,
            InstallerUrl64 = source.InstallerUrl64,
            InstallerUrlArm64 = source.InstallerUrlArm64,
            InstallerFileName = source.InstallerFileName,
            Sha256 = source.Sha256,
            Sha25632 = source.Sha25632,
            Sha25664 = source.Sha25664,
            Command = source.Command,
            SilentCommand = source.SilentCommand,
            Arguments = source.Arguments,
            SilentArguments = source.SilentArguments,
            SilentArgumentsArm64 = source.SilentArgumentsArm64,
            Architecture = source.Architecture,
            HasArm64Support = source.HasArm64Support,
            PortableArchiveUrl = source.PortableArchiveUrl,
            PortableExecutable = source.PortableExecutable,
            PortableArchiveType = source.PortableArchiveType,
            PortableSubfolder = source.PortableSubfolder,
            VirusTotalUrl = source.VirusTotalUrl,
            VirusTotalRatio = source.VirusTotalRatio,
            VirusTotalScanDate = source.VirusTotalScanDate,
            PreInstallScript = source.PreInstallScript,
            PostInstallScript = source.PostInstallScript,
            RequiresRestart = source.RequiresRestart,
            RequiresElevation = source.RequiresElevation,
            NeedsManualInstall = source.NeedsManualInstall,
            VerificationTimeoutSeconds = source.VerificationTimeoutSeconds,
            DetectDisplayNameContains = source.DetectDisplayNameContains,
            DetectExecutable = source.DetectExecutable
        };
    }

    private static bool ShouldPreferCommandForUpdate(InstallDefinition? installDefinition)
    {
        if (installDefinition is null)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(installDefinition.Command) &&
               installDefinition.Command.IndexOf("winget", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
