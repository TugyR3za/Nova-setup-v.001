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

    private static readonly Regex WingetListSplitRegex = new(@"\s{2,}", RegexOptions.Compiled);

    private static readonly Regex AnsiEscapeRegex = new(@"\x1B\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);

    private readonly LoggingService? _loggingService;

    public AppUpdateService(LoggingService? loggingService = null)
    {
        _loggingService = loggingService;
    }

    public async Task<Dictionary<string, string>> ResolveLatestCatalogVersionsAsync(
        IEnumerable<AppItem> allApps,
        string currentPlatform,
        CancellationToken cancellationToken = default)
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

        // Build a map of winget package IDs to app IDs for installed apps that need scanning
        var packageIdToAppId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
            if (!string.IsNullOrWhiteSpace(packageId))
            {
                packageIdToAppId[packageId] = app.Id;
            }
        }

        if (packageIdToAppId.Count == 0)
        {
            return resolvedVersions;
        }

        // FAST PATH: Single `winget upgrade` call to get all available updates at once
        // This replaces N individual `winget show --id X` calls — huge speed improvement
        var batchUpgrades = await TryBatchResolveUpgradesAsync(wingetPath, cancellationToken);
        foreach (var (packageId, appId) in packageIdToAppId)
        {
            if (batchUpgrades.TryGetValue(packageId, out var availableVersion) &&
                !string.IsNullOrWhiteSpace(availableVersion))
            {
                resolvedVersions[appId] = availableVersion;
                continue;
            }

            // SLOW FALLBACK: Only for packages not found in the batch result
            var latestVersion = await TryResolveLatestWingetVersionAsync(
                wingetPath,
                packageId,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(latestVersion))
            {
                resolvedVersions[appId] = latestVersion;
            }
        }

        if (resolvedVersions.Count > 0)
        {
            _loggingService?.LogInfo(
                $"[AppUpdateService] Refreshed latest package versions for {resolvedVersions.Count} app(s) from Winget.");
        }

        return resolvedVersions;
    }

    /// <summary>
    /// Runs a single "winget upgrade" command to discover all available updates at once.
    /// Returns a dictionary mapping winget package IDs to their available (latest) versions.
    /// This is dramatically faster than calling "winget show --id X" once per app.
    /// </summary>
    private async Task<Dictionary<string, string>> TryBatchResolveUpgradesAsync(
        string wingetPath,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = wingetPath,
                    Arguments = "upgrade --accept-source-agreements --disable-interactivity",
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
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(30));
            try
            {
                await process.WaitForExitAsync(timeoutSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                _loggingService?.LogWarning("[AppUpdateService] Batch winget upgrade timed out after 30s.");
                return results;
            }

            var output = await stdoutTask;
            _ = await stderrTask;
            output = AnsiEscapeRegex.Replace(output, string.Empty);

            // Parse the tabular output: Name | Id | Version | Available | Source
            var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var line in lines)
            {
                // Skip header/separator/footer lines
                if (line.StartsWith("Name", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("-", StringComparison.Ordinal) ||
                    line.Contains("upgrades available", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var columns = WingetListSplitRegex.Split(line);
                // Expected: Name, Id, Version, Available, Source (5 columns)
                if (columns.Length >= 4)
                {
                    var id = columns[1].Trim();
                    var availableVersion = columns.Length >= 5 ? columns[3].Trim() : columns[2].Trim();
                    if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(availableVersion))
                    {
                        results[id] = availableVersion;
                    }
                }
            }

            _loggingService?.LogInfo(
                $"[AppUpdateService] Batch winget upgrade found {results.Count} package(s) with available updates.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _loggingService?.LogWarning(
                $"[AppUpdateService] Batch winget upgrade failed, will fall back to individual queries: {ex.Message}");
        }

        return results;
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

        var results = await installerService.UpdateMultipleAsync(new[] { app }, CancellationToken.None);

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
        ArgumentNullException.ThrowIfNull(installerService);

        await installerService.UpdateMultipleAsync(apps ?? new List<AppItem>(), CancellationToken.None);
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
            return PlatformService.MacOS;
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

    private async Task<string> TryResolveLatestWingetVersionAsync(
        string wingetPath,
        string packageId,
        CancellationToken cancellationToken)
    {
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
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(12));
            try
            {
                await process.WaitForExitAsync(timeoutSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
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
                return string.Empty;
            }

            var output = $"{await stdoutTask}{Environment.NewLine}{await stderrTask}";
            var versionMatch = VersionLineRegex.Match(output);
            if (!versionMatch.Success)
            {
                _loggingService?.LogDebug(
                    $"[AppUpdateService] Winget did not return a latest version for package {packageId}.");
                return string.Empty;
            }

            var latestVersion = versionMatch.Groups["value"].Value.Trim();
            if (string.IsNullOrWhiteSpace(latestVersion))
            {
                return string.Empty;
            }

            _loggingService?.LogDebug(
                $"[AppUpdateService] Latest Winget version for {packageId}: {latestVersion}");
            return latestVersion;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _loggingService?.LogWarning(
                $"[AppUpdateService] Unable to refresh latest version for Winget package {packageId}: {ex.Message}");
            return string.Empty;
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
            LogoUrl = source.LogoUrl,
            WingetId = source.WingetId,
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
                Linux = source.SupportedPlatforms.Linux,
                MacOS = source.SupportedPlatforms.MacOS
            },
            WindowsInstall = CloneInstallDefinition(source.WindowsInstall),
            LinuxInstall = CloneInstallDefinition(source.LinuxInstall),
            MacOSInstall = CloneInstallDefinition(source.MacOSInstall),
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
