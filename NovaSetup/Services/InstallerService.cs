using System.Diagnostics;
using System.ComponentModel;
using System.Net.Http;
using System.Security.Principal;
using Avalonia.Threading;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class InstallerService
{
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();

    private readonly LoggingService? _loggingService;
    private readonly DetectionService? _detectionService;
    private readonly HistoryService? _historyService;
    private readonly DependencyResolverService _dependencyResolverService;
    private readonly HashVerificationService _hashVerificationService;
    private readonly PortableAppService _portableAppService;
    private readonly ScriptRunnerService? _scriptRunnerService;
    private readonly AppSettings? _settings;
    private readonly Dictionary<string, CancellationTokenSource> _appCancellationTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Process> _activeInstallProcesses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _activeInstallNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _activeInstallLock = new();

    public event Action<AppItem>? AppInstallStarted;
    public event Action<AppItem>? AppInstallCompleted;

    public Func<AppSettings?>? SettingsAccessor { get; set; }

    public InstallerService(
        LoggingService? loggingService = null,
        DetectionService? detectionService = null,
        HistoryService? historyService = null,
        ScriptRunnerService? scriptRunnerService = null)
    {
        _loggingService = loggingService;
        _detectionService = detectionService;
        _historyService = historyService;
        _dependencyResolverService = new DependencyResolverService(loggingService);
        _hashVerificationService = new HashVerificationService(loggingService);
        _portableAppService = new PortableAppService(loggingService);
        _scriptRunnerService = scriptRunnerService ?? new ScriptRunnerService(loggingService);
        _scriptRunnerService.SettingsAccessor ??= () => _settings ?? SettingsAccessor?.Invoke() ?? AppSettings.CreateDefault();
    }

    public InstallerService(
        AppSettings? settings,
        LoggingService? loggingService = null,
        ScriptRunnerService? scriptRunnerService = null)
        : this(loggingService, null, null, scriptRunnerService)
    {
        _settings = settings;
    }

    public void CancelApp(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return;
        }

        CancellationTokenSource? cancellationSource;
        string appName;

        lock (_activeInstallLock)
        {
            _appCancellationTokens.TryGetValue(appId, out cancellationSource);
            appName = _activeInstallNames.TryGetValue(appId, out var activeName)
                ? activeName
                : appId;
        }

        if (cancellationSource is null)
        {
            _loggingService?.LogWarning($"[Installer] No active install found for: {appId}");
            return;
        }

        _loggingService?.LogWarning($"[Installer] Cancel requested for: {appName}");

        try
        {
            cancellationSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The install already completed while the cancel request was in flight.
        }

        TryKillActiveProcess(appId);
    }

    public void CancelAllActiveInstalls()
    {
        foreach (var appId in AllowedCommandsService.Instance.GetActiveInstalls())
        {
            CancelApp(appId);
        }
    }

    public async Task<IReadOnlyList<InstallResult>> InstallSelectedAppsAsync(
        IEnumerable<AppItem> selectedApps,
        string currentPlatform,
        bool silentInstallEnabled,
        bool keepInstallersAfterInstall = false,
        string downloadLocationMode = AppSettings.DownloadSystemDefault,
        string customDownloadFolder = "",
        IEnumerable<AppItem>? catalogApps = null,
        bool allowUpgradeForInstalledApps = false,
        IProgress<InstallQueueProgress>? queueProgress = null,
        CancellationToken cancellationToken = default)
    {
        var source = selectedApps?.ToList() ?? new List<AppItem>();
        var results = new List<InstallResult>();
        var catalogSource = (catalogApps ?? source).ToList();
        var allAppsById = catalogSource
            .Where(app => app is not null && !string.IsNullOrWhiteSpace(app.Id))
            .GroupBy(app => app.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var requestedIds = source
            .Where(app => app is not null && !string.IsNullOrWhiteSpace(app.Id))
            .Select(app => app.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _loggingService?.LogInfo(
            $"Install started. Platform={currentPlatform}, CandidateCount={source.Count}, Silent={silentInstallEnabled}.");

        var orderedIds = await Task.Run(
            () => _dependencyResolverService.ResolveBuildOrder(requestedIds, allAppsById),
            cancellationToken);
        var missingDependencies = _detectionService is null
            ? new List<AppItem>()
            : await Task.Run(
                () => _dependencyResolverService.GetMissingDependencies(requestedIds, allAppsById, _detectionService),
                cancellationToken);
        var requestedSet = new HashSet<string>(requestedIds, StringComparer.OrdinalIgnoreCase);
        var missingDependencySet = new HashSet<string>(
            missingDependencies.Select(app => app.Id),
            StringComparer.OrdinalIgnoreCase);
        var installQueueIds = orderedIds
            .Where(id => requestedSet.Contains(id) || missingDependencySet.Contains(id))
            .ToList();

        foreach (var dependency in missingDependencies)
        {
            var requesterName = FindDependencyRequesterName(dependency.Id, requestedIds, allAppsById);
            _loggingService?.LogInfo(
                $"[DependencyResolver] Auto-adding dependency: {dependency.Name} required by {requesterName}");
        }

        if (installQueueIds.Count > 0)
        {
            var orderText = string.Join(
                " -> ",
                installQueueIds.Select(id => allAppsById.TryGetValue(id, out var app) ? app.Name : id));
            _loggingService?.LogInfo($"[DependencyResolver] Install order resolved: {orderText}");
        }

        var plan = PrepareInstallPlan(
            installQueueIds,
            allAppsById,
            currentPlatform,
            silentInstallEnabled,
            allowUpgradeForInstalledApps,
            results,
            queueProgress);
        if (plan.Count == 0)
        {
            _loggingService?.LogInfo("No install actions generated.");
            return results;
        }

        var rootDirectory = ResolveDownloadRoot(downloadLocationMode, customDownloadFolder);
        var tempRoot = Path.Combine(
            rootDirectory,
            "NovaSetup",
            $"install-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        var downloadedFiles = new List<string>();
        try
        {
            foreach (var action in plan)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await InstallAppAsync(action, currentPlatform, tempRoot, downloadedFiles, queueProgress, cancellationToken);
                results.Add(result);
            }
        }
        finally
        {
            if (keepInstallersAfterInstall)
            {
                _loggingService?.LogInfo($"Keeping downloaded installers in {tempRoot}");
            }
            else
            {
                await CleanupTemporaryFilesAsync(tempRoot, downloadedFiles);
            }
        }

        _loggingService?.LogInfo($"Install finished. ResultCount={results.Count}.");
        return results;
    }

    private List<InstallAction> PrepareInstallPlan(
        IEnumerable<string> orderedAppIds,
        IReadOnlyDictionary<string, AppItem> allAppsById,
        string currentPlatform,
        bool silentInstallEnabled,
        bool allowUpgradeForInstalledApps,
        List<InstallResult> results,
        IProgress<InstallQueueProgress>? queueProgress)
    {
        var plan = new List<InstallAction>();

        foreach (var appId in orderedAppIds)
        {
            if (!allAppsById.TryGetValue(appId, out var app))
            {
                continue;
            }

            app.HasInstallFailed = false;

            if (!app.IsSupportedOnCurrentPlatform)
            {
                _loggingService?.LogWarning($"Skipping {app.Name}: unsupported on {currentPlatform}.");
                var skippedResult = CreateSkippedResult(app, "Unsupported on current OS.");
                results.Add(skippedResult);
                ReportQueueFinalStatus(queueProgress, skippedResult);
                continue;
            }

            if (app.WillBeSkipped)
            {
                _loggingService?.LogWarning($"Skipping {app.Name}: marked to skip.");
                var skippedResult = CreateSkippedResult(app, "Marked to be skipped.");
                results.Add(skippedResult);
                ReportQueueFinalStatus(queueProgress, skippedResult);
                continue;
            }

            var installDefinition = GetInstallDefinition(app, currentPlatform);
            if (installDefinition is null)
            {
                _loggingService?.LogError($"Skipping {app.Name}: missing install metadata for {currentPlatform}.");
                var failureResult = CreateFailureResult(app, "Missing install metadata for current OS.");
                results.Add(failureResult);
                ReportQueueFinalStatus(queueProgress, failureResult);
                app.HasInstallFailed = true;
                continue;
            }

            if (installDefinition.NeedsManualInstall)
            {
                _loggingService?.LogWarning($"Skipping {app.Name}: manual install required.");
                var skippedResult = CreateSkippedResult(app, "Requires manual install.");
                results.Add(skippedResult);
                ReportQueueFinalStatus(queueProgress, skippedResult);
                continue;
            }

            if (!allowUpgradeForInstalledApps && IsCurrentlyInstalled(app, currentPlatform))
            {
                _loggingService?.LogInfo($"Skipping {app.Name}: already installed on this PC.");
                var skippedResult = CreateSkippedResult(app, "Already installed on this PC.");
                results.Add(skippedResult);
                ReportQueueFinalStatus(queueProgress, skippedResult);
                continue;
            }

            var silentDisabledByUserPreference = silentInstallEnabled && app.UserDisabledSilentInstall;
            if (silentDisabledByUserPreference)
            {
                _loggingService?.LogInfo(
                    $"[Installer] Silent install disabled for {app.Name} by user preference - running interactive install");
            }

            if (silentInstallEnabled && !silentDisabledByUserPreference && !app.SupportsSilentInstall)
            {
                _loggingService?.LogWarning(
                    $"Skipping {app.Name}: silent installation is enabled, but this app requires an interactive installer.");
                var skippedResult = CreateSkippedResult(
                    app,
                    "Silent installation is enabled, but this app requires manual installer interaction. Turn off Silent installation to install it.");
                results.Add(skippedResult);
                ReportQueueFinalStatus(queueProgress, skippedResult);
                continue;
            }

            var useSilent = silentInstallEnabled && !app.UserDisabledSilentInstall && app.SupportsSilentInstall;
            plan.Add(new InstallAction(app, installDefinition, useSilent));
        }

        return plan;
    }

    private static string FindDependencyRequesterName(
        string dependencyId,
        IEnumerable<string> requestedIds,
        IReadOnlyDictionary<string, AppItem> allAppsById)
    {
        foreach (var requestedId in requestedIds)
        {
            if (!allAppsById.TryGetValue(requestedId, out var requestedApp))
            {
                continue;
            }

            if (DependsOn(requestedApp, dependencyId, allAppsById, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
            {
                return requestedApp.Name;
            }
        }

        return dependencyId;
    }

    private static bool DependsOn(
        AppItem app,
        string dependencyId,
        IReadOnlyDictionary<string, AppItem> allAppsById,
        HashSet<string> visited)
    {
        if (!visited.Add(app.Id))
        {
            return false;
        }

        foreach (var candidateDependencyId in app.Dependencies)
        {
            if (candidateDependencyId.Equals(dependencyId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (allAppsById.TryGetValue(candidateDependencyId, out var dependencyApp) &&
                DependsOn(dependencyApp, dependencyId, allAppsById, visited))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<InstallResult> InstallAppAsync(
        InstallAction action,
        string currentPlatform,
        string tempRoot,
        List<string> downloadedFiles,
        IProgress<InstallQueueProgress>? queueProgress,
        CancellationToken cancellationToken)
    {
        if (!AllowedCommandsService.Instance.TryBeginInstall(action.App.Id))
        {
            _loggingService?.LogWarning(
                $"[AllowedCommands] Skipped duplicate install request for: {action.App.Name} (already in progress)");
            var skippedResult = CreateSkippedResult(action.App, "Install already in progress.");
            ReportQueueFinalStatus(queueProgress, skippedResult);
            return skippedResult;
        }

        using var appCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        RegisterAppCancellation(action.App, appCancellationSource);
        AppInstallStarted?.Invoke(action.App);

        var stopwatch = Stopwatch.StartNew();
        var exitCode = -1;
        string? command = null;
        string installMethod = "manual";
        var appCancellationToken = appCancellationSource.Token;
        var postInstallScript = action.InstallDefinition.PostInstallScript;

        try
        {
            _loggingService?.LogInfo($"Installing app: {action.App.Name}");
            var wasInstalledBefore = IsCurrentlyInstalled(action.App, currentPlatform);

            if (!string.IsNullOrWhiteSpace(action.InstallDefinition.PreInstallScript))
            {
                await RunInstallScriptAsync(
                    action.InstallDefinition.PreInstallScript,
                    action.App,
                    "pre",
                    appCancellationToken);
            }

            if (OperatingSystem.IsWindows() &&
                currentPlatform == PlatformService.Windows &&
                action.App.IsPortable &&
                !string.IsNullOrWhiteSpace(action.InstallDefinition.PortableArchiveUrl))
            {
                installMethod = "direct";
                var portableDestinationFolder = ResolvePortableDestinationFolder(action.App);
                if (string.IsNullOrWhiteSpace(portableDestinationFolder))
                {
                    action.App.HasInstallFailed = true;
                    return await FinalizeInstallResultAsync(
                        action.App,
                        currentPlatform,
                        installMethod,
                        stopwatch.ElapsedMilliseconds,
                        CreateFailureResult(action.App, "Portable apps folder is not configured."),
                        queueProgress);
                }

                ReportQueueProgress(queueProgress, action.App.Id, InstallQueueStatus.Downloading, "Downloading...", 0);
                var portableInstalled = await _portableAppService.InstallPortableAsync(
                    action.App,
                    portableDestinationFolder);
                if (!portableInstalled)
                {
                    action.App.HasInstallFailed = true;
                    return await FinalizeInstallResultAsync(
                        action.App,
                        currentPlatform,
                        installMethod,
                        stopwatch.ElapsedMilliseconds,
                        CreateFailureResult(action.App, "Portable app extraction failed."),
                        queueProgress);
                }

                ReportQueueProgress(queueProgress, action.App.Id, InstallQueueStatus.Installing, "Installing...", 0.6);
                if (_detectionService is not null)
                {
                    _detectionService.IsAppInstalled(action.App, currentPlatform);
                }

                action.App.IsInstalled = true;
                action.App.HasInstallFailed = false;
                action.App.RequiresRestartHint = false;
                action.App.StatusBadge = AppItem.StatusInstalled;

                return await FinalizeInstallResultAsync(
                    action.App,
                    currentPlatform,
                    installMethod,
                    stopwatch.ElapsedMilliseconds,
                    new InstallResult
                    {
                        AppId = action.App.Id,
                        AppName = action.App.Name,
                        Success = true,
                        Skipped = false,
                        RequiresRestart = false,
                        Message = wasInstalledBefore
                            ? "Portable app refreshed successfully."
                            : "Portable app extracted successfully."
                    },
                    queueProgress);
            }

            string? installerPath = null;
            if (HasInstallerDownloadUrl(action.InstallDefinition))
            {
                var download = await DownloadInstallerAsync(action, tempRoot, queueProgress, appCancellationToken);
                if (download.Cancelled)
                {
                    return await FinalizeInstallResultAsync(
                        action.App,
                        currentPlatform,
                        installMethod,
                        stopwatch.ElapsedMilliseconds,
                        CreateCancelledResult(action.App, download.Message),
                        queueProgress);
                }

                if (!download.Success)
                {
                    if (HasCommandFallback(action.InstallDefinition))
                    {
                        _loggingService?.LogWarning(
                            $"Download-first install failed for {action.App.Name}. Falling back to configured command. Reason: {download.Message}");
                    }
                    else
                    {
                        action.App.HasInstallFailed = true;
                        installMethod = DetermineInstallMethod(null, installerPath);
                        return await FinalizeInstallResultAsync(
                            action.App,
                            currentPlatform,
                            installMethod,
                            stopwatch.ElapsedMilliseconds,
                            CreateFailureResult(action.App, AppendElevationDeniedMessage(download.Message)),
                            queueProgress);
                    }
                }

                installerPath = download.FilePath;
                if (!string.IsNullOrWhiteSpace(installerPath))
                {
                    downloadedFiles.Add(installerPath);

                    if (!_hashVerificationService.VerifyFile(installerPath, ResolveInstallerSha256(action.InstallDefinition)))
                    {
                        _loggingService?.LogError(
                            $"[Installer] Aborting install for {action.App.Name} — SHA256 verification failed. The file may be corrupted or tampered with.");

                        try
                        {
                            File.Delete(installerPath);
                        }
                        catch
                        {
                            // Deletion failure is non-fatal; aborting execution is the important part.
                        }

                        action.App.HasInstallFailed = true;
                        installMethod = DetermineInstallMethod(null, installerPath);
                        return await FinalizeInstallResultAsync(
                            action.App,
                            currentPlatform,
                            installMethod,
                            stopwatch.ElapsedMilliseconds,
                            CreateFailureResult(action.App, "SHA256 verification failed."),
                            queueProgress);
                    }

                    LogVirusTotalStatus(action.App, action.InstallDefinition);
                }
            }

            ReportQueueProgress(queueProgress, action.App.Id, InstallQueueStatus.Installing, "Installing...", 0.6);

            command = currentPlatform switch
            {
                PlatformService.Windows => BuildWindowsInstallCommand(action, installerPath),
                PlatformService.MacOS => BuildMacOSInstallCommand(action),
                _ => BuildLinuxInstallCommand(action, installerPath)
            };
            installMethod = DetermineInstallMethod(command, installerPath);

            if (string.IsNullOrWhiteSpace(command))
            {
                _loggingService?.LogError($"Install command is empty for {action.App.Name}.");
                action.App.HasInstallFailed = true;
                return await FinalizeInstallResultAsync(
                    action.App,
                    currentPlatform,
                    installMethod,
                    stopwatch.ElapsedMilliseconds,
                    CreateFailureResult(
                        action.App,
                        AppendElevationDeniedMessage("No valid install command could be built.")),
                    queueProgress);
            }

            var execution = await ExecuteInstallCommandAsync(action, command, currentPlatform, appCancellationToken);
            exitCode = execution.ExitCode;
            if (execution.Cancelled)
            {
                return await FinalizeInstallResultAsync(
                    action.App,
                    currentPlatform,
                    installMethod,
                    stopwatch.ElapsedMilliseconds,
                    CreateCancelledResult(action.App, execution.Message),
                    queueProgress);
            }

            var restartRequired = DetermineIfRestartNeeded(action, execution.ExitCode);
            var wasVerifiedAfterInstall = false;
            if (!wasInstalledBefore)
            {
                wasVerifiedAfterInstall = await WaitForInstallVerificationAsync(action, currentPlatform, appCancellationToken);
            }

            if (!execution.Success)
            {
                if (!wasInstalledBefore && wasVerifiedAfterInstall)
                {
                    action.App.IsInstalled = true;
                    action.App.RequiresRestartHint = restartRequired;
                    action.App.StatusBadge = "Installed";

                    var recoveredMessage =
                        $"Installer reported exit code {execution.ExitCode}, but {action.App.Name} was verified as installed afterwards.";
                    _loggingService?.LogWarning(recoveredMessage);

                    return await FinalizeInstallResultAsync(
                        action.App,
                        currentPlatform,
                        installMethod,
                        stopwatch.ElapsedMilliseconds,
                        new InstallResult
                        {
                            AppId = action.App.Id,
                            AppName = action.App.Name,
                            Success = true,
                            Skipped = false,
                            RequiresRestart = restartRequired,
                            Message = recoveredMessage
                        },
                        queueProgress);
                }

                action.App.HasInstallFailed = true;
                var failureMessage = AppendElevationDeniedMessage(
                    BuildFailureMessage(action.App, currentPlatform, command, execution));
                _loggingService?.LogError(
                    $"Install failed for {action.App.Name}. ExitCode={execution.ExitCode}. {failureMessage}");

                return await FinalizeInstallResultAsync(
                    action.App,
                    currentPlatform,
                    installMethod,
                    stopwatch.ElapsedMilliseconds,
                    new InstallResult
                    {
                        AppId = action.App.Id,
                        AppName = action.App.Name,
                        Success = false,
                        Skipped = false,
                        RequiresRestart = restartRequired,
                        Message = failureMessage
                    },
                    queueProgress);
            }

            if (!wasInstalledBefore && !wasVerifiedAfterInstall)
            {
                action.App.HasInstallFailed = true;
                var verificationFailureMessage = AppendElevationDeniedMessage(
                    BuildVerificationFailureMessage(action.App, currentPlatform, command));
                _loggingService?.LogError(
                    $"Install could not be verified for {action.App.Name} after a successful exit code. {verificationFailureMessage}");

                return await FinalizeInstallResultAsync(
                    action.App,
                    currentPlatform,
                    installMethod,
                    stopwatch.ElapsedMilliseconds,
                    new InstallResult
                    {
                        AppId = action.App.Id,
                        AppName = action.App.Name,
                        Success = false,
                        Skipped = false,
                        RequiresRestart = restartRequired,
                        Message = verificationFailureMessage
                    },
                    queueProgress);
            }

            action.App.IsInstalled = true;
            action.App.RequiresRestartHint = restartRequired;
            action.App.StatusBadge = "Installed";

            _loggingService?.LogInfo($"Install succeeded for {action.App.Name}. ExitCode={execution.ExitCode}.");
            return await FinalizeInstallResultAsync(
                action.App,
                currentPlatform,
                installMethod,
                stopwatch.ElapsedMilliseconds,
                new InstallResult
                {
                    AppId = action.App.Id,
                    AppName = action.App.Name,
                    Success = true,
                    Skipped = false,
                    RequiresRestart = restartRequired,
                    Message = execution.RanElevated
                        ? "Install completed after administrator approval."
                        : execution.Message
                },
                queueProgress);
        }
        catch (OperationCanceledException)
        {
            _loggingService?.LogWarning($"Install cancelled for {action.App.Name}.");
            return await FinalizeInstallResultAsync(
                action.App,
                currentPlatform,
                installMethod,
                stopwatch.ElapsedMilliseconds,
                CreateCancelledResult(action.App, "Install cancelled."),
                queueProgress);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(postInstallScript))
            {
                await RunInstallScriptAsync(
                    postInstallScript,
                    action.App,
                    "post",
                    CancellationToken.None);
            }

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                action.App.DownloadProgressText = string.Empty;
                action.App.DownloadProgressPercent = 0;
                action.App.DownloadedBytes = 0;
                action.App.TotalBytes = 0;
            });
            _loggingService?.LogDebug($"[Installer] Process exit code for {action.App.Name}: {exitCode}");
            _loggingService?.LogDebug($"[Installer] Elapsed install time for {action.App.Name}: {stopwatch.ElapsedMilliseconds} ms");
            UnregisterActiveProcess(action.App.Id);
            UnregisterAppCancellation(action.App.Id, appCancellationSource);
            AllowedCommandsService.Instance.EndInstall(action.App.Id);
            AppInstallCompleted?.Invoke(action.App);
        }
    }

    private async Task<InstallResult> FinalizeInstallResultAsync(
        AppItem app,
        string currentPlatform,
        string installMethod,
        long elapsedMs,
        InstallResult result,
        IProgress<InstallQueueProgress>? queueProgress)
    {
        await RecordInstallHistoryAsync(app, currentPlatform, installMethod, result, elapsedMs);
        if (result.Success)
        {
            TryInvalidateDetectionCache();
        }

        ReportQueueFinalStatus(queueProgress, result);
        return result;
    }

    private void TryInvalidateDetectionCache()
    {
        try
        {
            _detectionService?.InvalidateDetectionCache();
        }
        catch
        {
            // Best-effort cache invalidation only.
        }
    }

    private async Task RunInstallScriptAsync(
        string scriptContentOrPath,
        AppItem app,
        string scriptPhase,
        CancellationToken cancellationToken)
    {
        if (_scriptRunnerService is null || string.IsNullOrWhiteSpace(scriptContentOrPath))
        {
            return;
        }

        if (!app.UserTrustedInstallScripts)
        {
            _loggingService?.LogInfo(
                $"[ScriptRunner] Install scripts are not enabled for {app.Name}. Skipping {scriptPhase} script.");
            return;
        }

        try
        {
            var scriptResult = await _scriptRunnerService.RunScriptAsync(
                scriptContentOrPath,
                app.Id,
                scriptPhase,
                cancellationToken);

            if (!scriptResult.Success)
            {
                _loggingService?.LogWarning(
                    $"[ScriptRunner] {scriptPhase} script for {app.Name} failed with exit code {scriptResult.ExitCode}. Install will continue.");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _loggingService?.LogWarning(
                $"[ScriptRunner] {scriptPhase} script for {app.Name} failed unexpectedly: {ex.Message}");
        }
    }

    private async Task RecordInstallHistoryAsync(
        AppItem app,
        string currentPlatform,
        string installMethod,
        InstallResult result,
        long elapsedMs)
    {
        if (_historyService is null || result.Skipped)
        {
            return;
        }

        await _historyService.RecordInstallAsync(new InstallRecord
        {
            AppId = app.Id,
            AppName = app.Name,
            Version = app.Version,
            Platform = currentPlatform,
            InstallMethod = string.IsNullOrWhiteSpace(installMethod) ? "manual" : installMethod,
            Success = result.Success,
            ErrorMessage = result.Success ? string.Empty : result.Message,
            InstalledAt = DateTime.UtcNow,
            ElapsedMs = elapsedMs
        });
    }

    private async Task<DownloadOutcome> DownloadInstallerAsync(
        InstallAction action,
        string tempRoot,
        IProgress<InstallQueueProgress>? queueProgress,
        CancellationToken cancellationToken)
    {
        var (installerUrl, selectedArchitecture) = ResolveInstallerUrl(action.InstallDefinition);
        if (string.IsNullOrWhiteSpace(installerUrl))
        {
            return DownloadOutcome.Fail("Installer URL is missing.");
        }

        _loggingService?.LogInfo($"[Installer] Using {selectedArchitecture} installer for {action.App.Name}");

        if (!Uri.TryCreate(installerUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            _loggingService?.LogWarning($"Invalid installer URL for {action.App.Name}: {installerUrl}");
            return DownloadOutcome.Fail("Installer URL is invalid.");
        }

        var preferredName = action.InstallDefinition.InstallerFileName;
        var fallbackName = Path.GetFileName(uri.LocalPath);
        var fileName = !string.IsNullOrWhiteSpace(preferredName)
            ? preferredName
            : (!string.IsNullOrWhiteSpace(fallbackName) ? fallbackName : $"{action.App.Id}-installer.bin");
        var safeName = string.Concat(fileName.Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = $"{action.App.Id}-installer.bin";
        }

        var targetPath = Path.Combine(tempRoot, safeName);

        try
        {
            _loggingService?.LogInfo($"Downloading installer for {action.App.Name} from {uri}");
            var downloadedPath = await DownloadInstallerAsync(action.App, installerUrl, targetPath, queueProgress, cancellationToken);
            _loggingService?.LogInfo($"Downloaded installer for {action.App.Name} to {targetPath}");
            return DownloadOutcome.Ok(downloadedPath);
        }
        catch (OperationCanceledException)
        {
            _loggingService?.LogWarning($"Download cancelled for {action.App.Name}.");
            return DownloadOutcome.CancelledResult("Download cancelled.");
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Download failed for {action.App.Name}: {ex.Message}");
            return DownloadOutcome.Fail($"Download failed: {ex.Message}");
        }
    }

    private async Task<string> DownloadInstallerAsync(AppItem app, string downloadUrl, string destinationPath)
    {
        return await DownloadInstallerAsync(app, downloadUrl, destinationPath, null, CancellationToken.None);
    }

    private async Task<string> DownloadInstallerAsync(
        AppItem app,
        string downloadUrl,
        string destinationPath,
        IProgress<InstallQueueProgress>? queueProgress,
        CancellationToken cancellationToken)
    {
        long totalBytes = 0;
        try
        {
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, downloadUrl);
            using var headResponse = await SharedHttpClient.SendAsync(headRequest, cancellationToken);
            if (headResponse.Content.Headers.ContentLength.HasValue)
            {
                totalBytes = headResponse.Content.Headers.ContentLength.Value;
            }
        }
        catch
        {
            // Content-Length not available — proceed without total.
        }

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            app.TotalBytes = totalBytes;
            app.DownloadedBytes = 0;
            app.DownloadProgressText = totalBytes > 0
                ? $"0 B / {FormatBytes(totalBytes)}"
                : "Starting download...";
        });
        ReportQueueProgress(queueProgress, app.Id, InstallQueueStatus.Downloading, "Downloading...", 0);

        using var response = await SharedHttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long downloadedBytes = 0;
        int bytesRead;
        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            downloadedBytes += bytesRead;
            var downloaded = downloadedBytes;
            var total = totalBytes;
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                app.DownloadedBytes = downloaded;
                app.DownloadProgressPercent = total > 0 ? (downloaded / (double)total) * 100.0 : 0;
                app.DownloadProgressText = total > 0
                    ? $"{FormatBytes(downloaded)} / {FormatBytes(total)}"
                    : $"{FormatBytes(downloaded)} downloaded";
            });

            var queueProgressValue = total > 0
                ? Math.Clamp((downloaded / (double)total) * 0.5, 0, 0.5)
                : 0;
            var statusText = total > 0
                ? $"Downloading... {Math.Round((downloaded / (double)total) * 100.0, 0):0}%"
                : "Downloading...";
            ReportQueueProgress(queueProgress, app.Id, InstallQueueStatus.Downloading, statusText, queueProgressValue);
        }

        return destinationPath;
    }

    private static (string Url, string SelectedArchitecture) ResolveInstallerUrl(InstallDefinition definition)
    {
        if (definition is null)
        {
            return (string.Empty, "universal");
        }

        if (PlatformService.IsArm64() && !string.IsNullOrWhiteSpace(definition.InstallerUrlArm64))
        {
            return (definition.InstallerUrlArm64, "arm64");
        }

        if (PlatformService.IsX64() && !string.IsNullOrWhiteSpace(definition.InstallerUrl64))
        {
            return (definition.InstallerUrl64, "x64");
        }

        if (!string.IsNullOrWhiteSpace(definition.InstallerUrl32))
        {
            return (definition.InstallerUrl32, "x86");
        }

        return (definition.InstallerUrl, "universal");
    }

    private static string ResolveInstallerSha256(InstallDefinition definition)
    {
        if (definition is null)
        {
            return string.Empty;
        }

        if (PlatformService.IsArm64() && !string.IsNullOrWhiteSpace(definition.InstallerUrlArm64))
        {
            return definition.Sha256;
        }

        return PlatformService.GetArchitecture() switch
        {
            "x86" when !string.IsNullOrWhiteSpace(definition.Sha25632) => definition.Sha25632,
            "x64" when !string.IsNullOrWhiteSpace(definition.Sha25664) => definition.Sha25664,
            _ => definition.Sha256
        };
    }

    private void LogVirusTotalStatus(AppItem app, InstallDefinition definition)
    {
        var ratio = definition?.VirusTotalRatio?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(ratio))
        {
            _loggingService?.LogInfo($"[VirusTotal] No VirusTotal data for {app.Name}");
            return;
        }

        if (TryParseVirusTotalDetections(ratio, out var detections))
        {
            if (detections == 0)
            {
                _loggingService?.LogInfo($"[VirusTotal] {app.Name} installer is clean ({ratio})");
            }
            else
            {
                _loggingService?.LogWarning(
                    $"[VirusTotal] WARNING: {app.Name} installer has {ratio} detections on VirusTotal. Proceeding with install as user requested.");
            }

            return;
        }

        _loggingService?.LogInfo($"[VirusTotal] No VirusTotal data for {app.Name}");
    }

    private static bool TryParseVirusTotalDetections(string ratio, out int detections)
    {
        detections = 0;
        if (string.IsNullOrWhiteSpace(ratio))
        {
            return false;
        }

        var separatorIndex = ratio.IndexOf('/');
        if (separatorIndex <= 0)
        {
            return false;
        }

        return int.TryParse(ratio[..separatorIndex].Trim(), out detections);
    }

    private static void ReportQueueProgress(
        IProgress<InstallQueueProgress>? queueProgress,
        string appId,
        InstallQueueStatus status,
        string statusText,
        double progress)
    {
        if (queueProgress is null || string.IsNullOrWhiteSpace(appId))
        {
            return;
        }

        queueProgress.Report(new InstallQueueProgress(appId, status, statusText, Math.Clamp(progress, 0, 1)));
    }

    private static void ReportQueueFinalStatus(IProgress<InstallQueueProgress>? queueProgress, InstallResult result)
    {
        if (queueProgress is null || result is null || string.IsNullOrWhiteSpace(result.AppId))
        {
            return;
        }

        var (status, statusText) = result switch
        {
            { Cancelled: true } => (InstallQueueStatus.Cancelled, "Cancelled"),
            { Skipped: true } => (InstallQueueStatus.Skipped, "Skipped"),
            { Success: true } => (InstallQueueStatus.Done, "Done ✓"),
            _ => (InstallQueueStatus.Failed, "Failed")
        };

        queueProgress.Report(new InstallQueueProgress(result.AppId, status, statusText, 1.0));
    }

    private async Task<ExecutionOutcome> ExecuteInstallCommandAsync(
        InstallAction action,
        string command,
        string currentPlatform,
        CancellationToken cancellationToken)
    {
        var runElevatedFirst = ShouldRunElevatedFirst(action, command, currentPlatform);
        if (runElevatedFirst)
        {
            _loggingService?.LogInfo(
                $"Install for {action.App.Name} is configured to request administrator approval before running.");
            return await ExecuteElevatedInstallCommandAsync(action, command, cancellationToken);
        }

        var execution = await ExecuteProcessInstallCommandAsync(action, command, currentPlatform, cancellationToken);
        if (ShouldRetryElevated(action, command, currentPlatform, execution))
        {
            _loggingService?.LogWarning(
                $"Install for {action.App.Name} failed without elevation (ExitCode={execution.ExitCode}). Retrying with administrator approval.");
            return await ExecuteElevatedInstallCommandAsync(action, command, cancellationToken);
        }

        return execution;
    }

    private async Task<ExecutionOutcome> ExecuteProcessInstallCommandAsync(
        InstallAction action,
        string command,
        string currentPlatform,
        CancellationToken cancellationToken)
    {
        Process? process = null;
        try
        {
            var startInfo = currentPlatform == PlatformService.Windows
                ? new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
                : new ProcessStartInfo
                {
                    FileName = "sh",
                    Arguments = $"-c \"{command.Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

            _loggingService?.LogInfo($"Executing install command: {command}");
            _loggingService?.LogDebug($"[Installer] Full command: {command}");
            _loggingService?.LogDebug($"[Installer] Working directory: {ResolveWorkingDirectory(startInfo)}");
            process = new Process { StartInfo = startInfo };
            process.Start();
            RegisterActiveProcess(action.App.Id, process);

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var stdout = (await stdoutTask).Trim();
            var stderr = (await stderrTask).Trim();
            var exitCode = process.ExitCode;

            _loggingService?.LogInfo($"Command completed. ExitCode={exitCode}");
            _loggingService?.LogDebug($"[Installer] Process exit code: {exitCode}");
            if (!string.IsNullOrWhiteSpace(stdout))
            {
                _loggingService?.LogInfo($"Command output: {TrimForLog(stdout)}");
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                _loggingService?.LogWarning($"Command error output: {TrimForLog(stderr)}");
            }

            var success = exitCode == 0 || exitCode == 3010 || exitCode == 1641;
            var message = success
                ? "Install command completed successfully."
                : (string.IsNullOrWhiteSpace(stderr) ? $"Install command failed with exit code {exitCode}." : stderr);

            return new ExecutionOutcome(success, exitCode, message, RanElevated: false, ElevationCancelled: false, Cancelled: false);
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            _loggingService?.LogWarning("Install command cancelled.");
            return new ExecutionOutcome(false, -1, "Install command cancelled.", RanElevated: false, ElevationCancelled: false, Cancelled: true);
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Install command execution failed: {ex.Message}");
            return new ExecutionOutcome(false, -1, $"Install command execution failed: {ex.Message}", RanElevated: false, ElevationCancelled: false, Cancelled: false);
        }
        finally
        {
            UnregisterActiveProcess(action.App.Id, process);
            process?.Dispose();
        }
    }

    private async Task<ExecutionOutcome> ExecuteElevatedInstallCommandAsync(
        InstallAction action,
        string command,
        CancellationToken cancellationToken)
    {
        Process? process = null;
        try
        {
            var startInfo = BuildElevatedStartInfo(command);

            _loggingService?.LogInfo($"Executing install command with administrator approval: {command}");
            _loggingService?.LogDebug($"[Installer] Full command: {command}");
            _loggingService?.LogDebug($"[Installer] Working directory: {ResolveWorkingDirectory(startInfo)}");
            process = new Process { StartInfo = startInfo };
            process.Start();
            RegisterActiveProcess(action.App.Id, process);
            await process.WaitForExitAsync(cancellationToken);

            var exitCode = process.ExitCode;
            _loggingService?.LogInfo($"Elevated command completed. ExitCode={exitCode}");
            _loggingService?.LogDebug($"[Installer] Process exit code: {exitCode}");

            var success = exitCode == 0 || exitCode == 3010 || exitCode == 1641;
            var message = success
                ? "Install command completed successfully."
                : $"Install command failed with exit code {exitCode}.";

            return new ExecutionOutcome(success, exitCode, message, RanElevated: true, ElevationCancelled: false, Cancelled: false);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            const string message = "Windows permission prompt was cancelled. The install did not continue.";
            _loggingService?.LogWarning(message);
            return new ExecutionOutcome(false, 1223, message, RanElevated: true, ElevationCancelled: true, Cancelled: false);
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            _loggingService?.LogWarning("Elevated install command cancelled.");
            return new ExecutionOutcome(false, -1, "Install command cancelled.", RanElevated: true, ElevationCancelled: false, Cancelled: true);
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Elevated install command execution failed: {ex.Message}");
            return new ExecutionOutcome(false, -1, $"Install command execution failed: {ex.Message}", RanElevated: true, ElevationCancelled: false, Cancelled: false);
        }
        finally
        {
            UnregisterActiveProcess(action.App.Id, process);
            process?.Dispose();
        }
    }

    private static ProcessStartInfo BuildElevatedStartInfo(string command)
    {
        if (TryBuildDirectElevatedStartInfo(command, out var directStartInfo))
        {
            return directStartInfo;
        }

        return new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            Arguments = $"/c {command}",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
    }

    private static bool TryBuildDirectElevatedStartInfo(string command, out ProcessStartInfo startInfo)
    {
        startInfo = default!;
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var trimmed = command.Trim();
        if (trimmed.StartsWith("winget ", StringComparison.OrdinalIgnoreCase))
        {
            startInfo = new ProcessStartInfo
            {
                FileName = ResolveWingetExecutablePath(),
                Arguments = trimmed["winget".Length..].Trim(),
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            return true;
        }

        if (trimmed.StartsWith("msiexec ", StringComparison.OrdinalIgnoreCase))
        {
            startInfo = new ProcessStartInfo
            {
                FileName = "msiexec.exe",
                Arguments = trimmed["msiexec".Length..].Trim(),
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            return true;
        }

        if (TrySplitExecutableCommand(trimmed, out var fileName, out var arguments))
        {
            startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            return true;
        }

        return false;
    }

    private static bool TrySplitExecutableCommand(string command, out string fileName, out string arguments)
    {
        fileName = string.Empty;
        arguments = string.Empty;

        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var trimmed = command.Trim();
        if (trimmed.StartsWith("\"", StringComparison.Ordinal))
        {
            var closingQuote = trimmed.IndexOf('"', 1);
            if (closingQuote <= 1)
            {
                return false;
            }

            fileName = trimmed[1..closingQuote];
            arguments = trimmed[(closingQuote + 1)..].Trim();
        }
        else
        {
            var firstSpace = trimmed.IndexOf(' ');
            if (firstSpace < 0)
            {
                fileName = trimmed;
            }
            else
            {
                fileName = trimmed[..firstSpace];
                arguments = trimmed[(firstSpace + 1)..].Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);
        return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".msi", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveWingetExecutablePath()
    {
        var localAlias = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "WindowsApps",
            "winget.exe");

        return string.IsNullOrWhiteSpace(localAlias) ? "winget.exe" : localAlias;
    }

    private string BuildWindowsInstallCommand(InstallAction action, string? installerPath)
    {
        if (!string.IsNullOrWhiteSpace(installerPath))
        {
            var ext = Path.GetExtension(installerPath).ToLowerInvariant();
            if (ext == ".msi")
            {
                var msiArgs = action.UseSilent
                    ? FirstNonEmpty(ResolveSilentArguments(action.InstallDefinition), "/qn /norestart")
                    : action.InstallDefinition.Arguments;
                return $"msiexec /i \"{installerPath}\" {msiArgs}".Trim();
            }

            var args = action.UseSilent ? ResolveSilentArguments(action.InstallDefinition) : action.InstallDefinition.Arguments;
            return $"\"{installerPath}\" {args}".Trim();
        }

        var command = action.UseSilent && !string.IsNullOrWhiteSpace(action.InstallDefinition.SilentCommand)
            ? action.InstallDefinition.SilentCommand
            : action.InstallDefinition.Command;

        return NormalizeWindowsCommand(command, action.UseSilent);
    }

    private string BuildLinuxInstallCommand(InstallAction action, string? installerPath)
    {
        if (!string.IsNullOrWhiteSpace(installerPath))
        {
            var ext = Path.GetExtension(installerPath).ToLowerInvariant();
            if (ext == ".deb")
            {
                return $"sudo dpkg -i \"{installerPath}\"";
            }

            var args = action.UseSilent ? action.InstallDefinition.SilentArguments : action.InstallDefinition.Arguments;
            return $"chmod +x \"{installerPath}\" && \"{installerPath}\" {args}".Trim();
        }

        if (action.UseSilent && !string.IsNullOrWhiteSpace(action.InstallDefinition.SilentCommand))
        {
            return action.InstallDefinition.SilentCommand;
        }

        return action.InstallDefinition.Command;
    }

    private string BuildMacOSInstallCommand(InstallAction action)
    {
        if (action.UseSilent && !string.IsNullOrWhiteSpace(action.InstallDefinition.SilentCommand))
        {
            return action.InstallDefinition.SilentCommand;
        }

        if (!string.IsNullOrWhiteSpace(action.InstallDefinition.Command))
        {
            return action.InstallDefinition.Command;
        }

        return $"brew install {action.App.Id}";
    }

    private bool DetermineIfRestartNeeded(InstallAction action, int exitCode)
    {
        return action.InstallDefinition.RequiresRestart ||
               action.App.RequiresRestartHint ||
               exitCode == 3010 ||
               exitCode == 1641;
    }

    private bool IsCurrentlyInstalled(AppItem app, string currentPlatform)
    {
        return _detectionService?.IsAppInstalled(app, currentPlatform) ?? app.IsInstalled;
    }

    private async Task<bool> WaitForInstallVerificationAsync(
        InstallAction action,
        string currentPlatform,
        CancellationToken cancellationToken)
    {
        if (_detectionService is null)
        {
            return action.App.IsInstalled;
        }

        var timeoutSeconds = action.InstallDefinition.VerificationTimeoutSeconds > 0
            ? action.InstallDefinition.VerificationTimeoutSeconds
            : 35;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var interval = TimeSpan.FromSeconds(2);
        var startedAt = DateTime.UtcNow;

        while (DateTime.UtcNow - startedAt < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_detectionService.IsAppInstalled(action.App, currentPlatform))
            {
                _loggingService?.LogInfo($"Verified installed app '{action.App.Name}' after command completion.");
                return true;
            }

            await Task.Delay(interval, cancellationToken);
        }

        return false;
    }

    private async Task CleanupTemporaryFilesAsync(string tempRoot, IEnumerable<string> downloadedFiles)
    {
        try
        {
            foreach (var file in downloadedFiles.Where(File.Exists))
            {
                File.Delete(file);
                _loggingService?.LogInfo($"Deleted temp installer file: {file}");
            }

            if (Directory.Exists(tempRoot))
            {
                await Task.Run(() => Directory.Delete(tempRoot, recursive: true));
                _loggingService?.LogInfo($"Deleted temp installer directory: {tempRoot}");
            }
        }
        catch (Exception ex)
        {
            _loggingService?.LogWarning($"Temporary file cleanup issue: {ex.Message}");
        }
    }

    private static InstallDefinition? GetInstallDefinition(AppItem app, string currentPlatform)
    {
        return currentPlatform switch
        {
            PlatformService.Windows => app.WindowsInstall,
            PlatformService.Linux => app.LinuxInstall,
            PlatformService.MacOS => app.MacOSInstall,
            _ => null
        };
    }

    private static InstallResult CreateSkippedResult(AppItem app, string reason)
    {
        return new InstallResult
        {
            AppId = app.Id,
            AppName = app.Name,
            Success = false,
            Skipped = true,
            Cancelled = false,
            RequiresRestart = false,
            Message = reason
        };
    }

    private static InstallResult CreateCancelledResult(AppItem app, string reason)
    {
        return new InstallResult
        {
            AppId = app.Id,
            AppName = app.Name,
            Success = false,
            Skipped = false,
            Cancelled = true,
            RequiresRestart = false,
            Message = reason
        };
    }

    private static InstallResult CreateFailureResult(AppItem app, string reason)
    {
        return new InstallResult
        {
            AppId = app.Id,
            AppName = app.Name,
            Success = false,
            Skipped = false,
            Cancelled = false,
            RequiresRestart = false,
            Message = reason
        };
    }

    private static string FirstNonEmpty(string primary, string fallback)
    {
        return string.IsNullOrWhiteSpace(primary) ? fallback : primary;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024L * 1024L)
        {
            return $"{bytes / 1024d:F1} KB";
        }

        if (bytes < 1024L * 1024L * 1024L)
        {
            return $"{bytes / (1024d * 1024d):F1} MB";
        }

        return $"{bytes / (1024d * 1024d * 1024d):F2} GB";
    }

    private static string ResolveSilentArguments(InstallDefinition definition)
    {
        if (PlatformService.IsArm64() && !string.IsNullOrWhiteSpace(definition.SilentArgumentsArm64))
        {
            return definition.SilentArgumentsArm64;
        }

        return definition.SilentArguments;
    }

    private static bool HasCommandFallback(InstallDefinition definition)
    {
        return !string.IsNullOrWhiteSpace(definition.Command) ||
               !string.IsNullOrWhiteSpace(definition.SilentCommand);
    }

    private static string ResolveWorkingDirectory(ProcessStartInfo startInfo)
    {
        return string.IsNullOrWhiteSpace(startInfo.WorkingDirectory)
            ? Environment.CurrentDirectory
            : startInfo.WorkingDirectory;
    }

    private void RegisterAppCancellation(AppItem app, CancellationTokenSource cancellationSource)
    {
        lock (_activeInstallLock)
        {
            _appCancellationTokens[app.Id] = cancellationSource;
            _activeInstallNames[app.Id] = app.Name;
        }
    }

    private void UnregisterAppCancellation(string appId, CancellationTokenSource cancellationSource)
    {
        lock (_activeInstallLock)
        {
            if (_appCancellationTokens.TryGetValue(appId, out var existingSource) &&
                ReferenceEquals(existingSource, cancellationSource))
            {
                _appCancellationTokens.Remove(appId);
            }

            _activeInstallNames.Remove(appId);
        }
    }

    private void RegisterActiveProcess(string appId, Process process)
    {
        lock (_activeInstallLock)
        {
            _activeInstallProcesses[appId] = process;
        }
    }

    private void UnregisterActiveProcess(string appId, Process? process = null)
    {
        lock (_activeInstallLock)
        {
            if (_activeInstallProcesses.TryGetValue(appId, out var activeProcess) &&
                (process is null || ReferenceEquals(activeProcess, process)))
            {
                _activeInstallProcesses.Remove(appId);
            }
        }
    }

    private void TryKillActiveProcess(string appId)
    {
        Process? process;
        lock (_activeInstallLock)
        {
            _activeInstallProcesses.TryGetValue(appId, out process);
        }

        TryKillProcess(process);
    }

    private static void TryKillProcess(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort termination only.
        }
    }

    private static bool HasInstallerDownloadUrl(InstallDefinition definition)
    {
        return !string.IsNullOrWhiteSpace(definition.InstallerUrl) ||
               !string.IsNullOrWhiteSpace(definition.InstallerUrl32) ||
               !string.IsNullOrWhiteSpace(definition.InstallerUrl64) ||
               !string.IsNullOrWhiteSpace(definition.InstallerUrlArm64);
    }

    private string ResolvePortableDestinationFolder(AppItem app)
    {
        if (!string.IsNullOrWhiteSpace(app.PortableInstallPath))
        {
            var trimmedPath = app.PortableInstallPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(Path.GetFileName(trimmedPath), app.Id, StringComparison.OrdinalIgnoreCase))
            {
                return Directory.GetParent(trimmedPath)?.FullName ?? trimmedPath;
            }

            return trimmedPath;
        }

        var configuredPortableFolder = SettingsAccessor?.Invoke()?.DefaultPortableFolder
            ?? _settings?.DefaultPortableFolder
            ?? AppSettings.CreateDefault().DefaultPortableFolder;

        return string.IsNullOrWhiteSpace(configuredPortableFolder)
            ? string.Empty
            : configuredPortableFolder;
    }

    private static string DetermineInstallMethod(string? command, string? installerPath)
    {
        if (!string.IsNullOrWhiteSpace(installerPath))
        {
            return "direct";
        }

        if (IsWingetCommand(command ?? string.Empty))
        {
            return "winget";
        }

        return "manual";
    }

    private static string NormalizeWindowsCommand(string command, bool useSilent)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return command;
        }

        if (!command.TrimStart().StartsWith("winget ", StringComparison.OrdinalIgnoreCase))
        {
            return command;
        }

        var normalized = command.Trim();
        if (useSilent && !normalized.Contains("--silent", StringComparison.OrdinalIgnoreCase))
        {
            normalized += " --silent";
        }

        if (!normalized.Contains("--disable-interactivity", StringComparison.OrdinalIgnoreCase))
        {
            normalized += " --disable-interactivity";
        }

        if (!normalized.Contains("--accept-source-agreements", StringComparison.OrdinalIgnoreCase))
        {
            normalized += " --accept-source-agreements";
        }

        if (!normalized.Contains("--accept-package-agreements", StringComparison.OrdinalIgnoreCase))
        {
            normalized += " --accept-package-agreements";
        }

        return normalized;
    }

    private string BuildFailureMessage(AppItem app, string currentPlatform, string command, ExecutionOutcome execution)
    {
        if (execution.ElevationCancelled)
        {
            return $"{app.Name} requires Windows administrator approval to install. The permission prompt was cancelled, so the install did not continue.";
        }

        if (currentPlatform == PlatformService.Windows && IsWingetCommand(command))
        {
            var codeHex = FormatExitCodeAsHex(execution.ExitCode);
            if (execution.RanElevated || IsWindowsProcessElevated())
            {
                return $"winget reported an install failure for {app.Name} (0x{codeHex}) even after administrator approval. {execution.Message}";
            }

            if (!IsWindowsProcessElevated())
            {
                return $"winget reported an install failure for {app.Name} (0x{codeHex}). Silent install hides installer windows when possible, but it does not bypass Windows UAC permission prompts. If a Windows permission dialog appears, approve it or run NovaSetup as administrator.";
            }

            return $"winget reported an install failure for {app.Name} (0x{codeHex}). {execution.Message}";
        }

        return execution.Message;
    }

    private static bool ShouldRunElevatedFirst(InstallAction action, string command, string currentPlatform)
    {
        return currentPlatform == PlatformService.Windows &&
               !IsWindowsProcessElevated() &&
               action.InstallDefinition.RequiresElevation &&
               SupportsElevatedDirectExecution(command);
    }

    private static bool ShouldRetryElevated(
        InstallAction action,
        string command,
        string currentPlatform,
        ExecutionOutcome execution)
    {
        if (currentPlatform != PlatformService.Windows ||
            IsWindowsProcessElevated() ||
            execution.RanElevated ||
            execution.Success ||
            !SupportsElevatedDirectExecution(command))
        {
            return false;
        }

        if (action.InstallDefinition.RequiresElevation)
        {
            return true;
        }

        var code = unchecked((uint)execution.ExitCode);
        return code == 0x8A150006u || execution.ExitCode == 740;
    }

    private string BuildVerificationFailureMessage(AppItem app, string currentPlatform, string command)
    {
        if (currentPlatform == PlatformService.Windows && IsWingetCommand(command))
        {
            if (!IsWindowsProcessElevated())
            {
                return $"{app.Name} exited without a confirmed install footprint. This package may require Windows elevation even in silent mode. Run NovaSetup as administrator or install this app manually.";
            }

            return $"{app.Name} exited successfully, but NovaSetup could not verify that it was installed. The package may require a manual completion step.";
        }

        return $"{app.Name} exited successfully, but NovaSetup could not verify that it was installed.";
    }

    private string AppendElevationDeniedMessage(string message)
    {
        if (!ElevationService.ElevationWasDenied)
        {
            return message;
        }

        const string elevationDeniedMessage =
            "Install failed — Nova is not running as administrator. Please restart Nova and click Yes on the UAC prompt.";
        _loggingService?.LogError(elevationDeniedMessage);

        if (string.IsNullOrWhiteSpace(message))
        {
            return elevationDeniedMessage;
        }

        return $"{message} {elevationDeniedMessage}";
    }

    private static bool IsWingetCommand(string command)
    {
        return !string.IsNullOrWhiteSpace(command) &&
               command.TrimStart().StartsWith("winget ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SupportsElevatedDirectExecution(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var trimmed = command.TrimStart();
        return trimmed.StartsWith("winget ", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("msiexec ", StringComparison.OrdinalIgnoreCase) ||
               TrySplitExecutableCommand(trimmed, out _, out _);
    }

    private static string FormatExitCodeAsHex(int exitCode)
    {
        return unchecked((uint)exitCode).ToString("X8");
    }

    private static bool IsWindowsProcessElevated()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static string TrimForLog(string value)
    {
        if (value.Length <= 300)
            return value;

        // Avoid splitting UTF-16 surrogate pairs (emoji, CJK extension, etc.)
        var end = 300;
        if (end < value.Length && char.IsLowSurrogate(value[end]))
            end--;

        return string.Concat(value.AsSpan(0, end), "...");
    }

    private string ResolveDownloadRoot(string downloadLocationMode, string customDownloadFolder)
    {
        return downloadLocationMode switch
        {
            AppSettings.DownloadCustomFolder when !string.IsNullOrWhiteSpace(customDownloadFolder)
                => customDownloadFolder,
            AppSettings.DownloadAskEveryTime => LogAndUseSystemDefault("Download location is set to Ask every time. Using system default until folder-prompt support is added."),
            _ => Path.GetTempPath()
        };
    }

    private string LogAndUseSystemDefault(string message)
    {
        _loggingService?.LogInfo(message);
        return Path.GetTempPath();
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        return client;
    }

    private sealed record InstallAction(AppItem App, InstallDefinition InstallDefinition, bool UseSilent);

    private sealed record DownloadOutcome(bool Success, bool Cancelled, string? FilePath, string Message)
    {
        public static DownloadOutcome Ok(string filePath) => new(true, false, filePath, "Download succeeded.");

        public static DownloadOutcome Fail(string message) => new(false, false, null, message);

        public static DownloadOutcome CancelledResult(string message) => new(false, true, null, message);
    }

    private sealed record ExecutionOutcome(
        bool Success,
        int ExitCode,
        string Message,
        bool RanElevated,
        bool ElevationCancelled,
        bool Cancelled);
}
