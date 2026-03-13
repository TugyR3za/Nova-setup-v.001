using System.Diagnostics;
using System.ComponentModel;
using System.Net.Http;
using System.Security.Principal;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class InstallerService
{
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();

    private readonly LoggingService? _loggingService;
    private readonly DetectionService? _detectionService;

    public InstallerService(LoggingService? loggingService = null, DetectionService? detectionService = null)
    {
        _loggingService = loggingService;
        _detectionService = detectionService;
    }

    public async Task<IReadOnlyList<InstallResult>> InstallSelectedAppsAsync(
        IEnumerable<AppItem> selectedApps,
        string currentPlatform,
        bool silentInstallEnabled,
        bool keepInstallersAfterInstall = false,
        string downloadLocationMode = AppSettings.DownloadSystemDefault,
        string customDownloadFolder = "",
        CancellationToken cancellationToken = default)
    {
        var source = selectedApps?.ToList() ?? new List<AppItem>();
        var results = new List<InstallResult>();

        _loggingService?.LogInfo(
            $"Install started. Platform={currentPlatform}, CandidateCount={source.Count}, Silent={silentInstallEnabled}.");

        var plan = PrepareInstallPlan(source, currentPlatform, silentInstallEnabled, results);
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
                var result = await InstallAppAsync(action, currentPlatform, tempRoot, downloadedFiles, cancellationToken);
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
        IEnumerable<AppItem> selectedApps,
        string currentPlatform,
        bool silentInstallEnabled,
        List<InstallResult> results)
    {
        var plan = new List<InstallAction>();

        foreach (var app in selectedApps.Where(app => app.IsSelected))
        {
            app.HasInstallFailed = false;

            if (!app.IsSupportedOnCurrentPlatform)
            {
                _loggingService?.LogWarning($"Skipping {app.Name}: unsupported on {currentPlatform}.");
                results.Add(CreateSkippedResult(app, "Unsupported on current OS."));
                continue;
            }

            if (app.WillBeSkipped)
            {
                _loggingService?.LogWarning($"Skipping {app.Name}: marked to skip.");
                results.Add(CreateSkippedResult(app, "Marked to be skipped."));
                continue;
            }

            var installDefinition = GetInstallDefinition(app, currentPlatform);
            if (installDefinition is null)
            {
                _loggingService?.LogError($"Skipping {app.Name}: missing install metadata for {currentPlatform}.");
                results.Add(CreateFailureResult(app, "Missing install metadata for current OS."));
                app.HasInstallFailed = true;
                continue;
            }

            if (installDefinition.NeedsManualInstall)
            {
                _loggingService?.LogWarning($"Skipping {app.Name}: manual install required.");
                results.Add(CreateSkippedResult(app, "Requires manual install."));
                continue;
            }

            if (IsCurrentlyInstalled(app, currentPlatform))
            {
                _loggingService?.LogInfo($"Skipping {app.Name}: already installed on this PC.");
                results.Add(CreateSkippedResult(app, "Already installed on this PC."));
                continue;
            }

            if (silentInstallEnabled && !app.SupportsSilentInstall)
            {
                _loggingService?.LogWarning(
                    $"Skipping {app.Name}: silent installation is enabled, but this app requires an interactive installer.");
                results.Add(
                    CreateSkippedResult(
                        app,
                        "Silent installation is enabled, but this app requires manual installer interaction. Turn off Silent installation to install it."));
                continue;
            }

            var useSilent = silentInstallEnabled && app.SupportsSilentInstall;
            plan.Add(new InstallAction(app, installDefinition, useSilent));
        }

        return plan;
    }

    private async Task<InstallResult> InstallAppAsync(
        InstallAction action,
        string currentPlatform,
        string tempRoot,
        List<string> downloadedFiles,
        CancellationToken cancellationToken)
    {
        _loggingService?.LogInfo($"Installing app: {action.App.Name}");
        var wasInstalledBefore = IsCurrentlyInstalled(action.App, currentPlatform);

        string? installerPath = null;
        if (!string.IsNullOrWhiteSpace(action.InstallDefinition.InstallerUrl))
        {
            var download = await DownloadInstallerAsync(action, tempRoot, cancellationToken);
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
                    return CreateFailureResult(action.App, download.Message);
                }
            }

            installerPath = download.FilePath;
            if (!string.IsNullOrWhiteSpace(installerPath))
            {
                downloadedFiles.Add(installerPath);
            }
        }

        var command = currentPlatform == PlatformService.Windows
            ? BuildWindowsInstallCommand(action, installerPath)
            : BuildLinuxInstallCommand(action, installerPath);

        if (string.IsNullOrWhiteSpace(command))
        {
            _loggingService?.LogError($"Install command is empty for {action.App.Name}.");
            action.App.HasInstallFailed = true;
            return CreateFailureResult(action.App, "No valid install command could be built.");
        }

        var execution = await ExecuteInstallCommandAsync(action, command, currentPlatform, cancellationToken);
        var restartRequired = DetermineIfRestartNeeded(action, execution.ExitCode);
        var wasVerifiedAfterInstall = false;
        if (!wasInstalledBefore)
        {
            wasVerifiedAfterInstall = await WaitForInstallVerificationAsync(action, currentPlatform, cancellationToken);
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

                return new InstallResult
                {
                    AppId = action.App.Id,
                    AppName = action.App.Name,
                    Success = true,
                    Skipped = false,
                    RequiresRestart = restartRequired,
                    Message = recoveredMessage
                };
            }

            action.App.HasInstallFailed = true;
            var failureMessage = BuildFailureMessage(action.App, currentPlatform, command, execution);
            _loggingService?.LogError(
                $"Install failed for {action.App.Name}. ExitCode={execution.ExitCode}. {failureMessage}");

            return new InstallResult
            {
                AppId = action.App.Id,
                AppName = action.App.Name,
                Success = false,
                Skipped = false,
                RequiresRestart = restartRequired,
                Message = failureMessage
            };
        }

        if (!wasInstalledBefore && !wasVerifiedAfterInstall)
        {
            action.App.HasInstallFailed = true;
            var verificationFailureMessage = BuildVerificationFailureMessage(action.App, currentPlatform, command);
            _loggingService?.LogError(
                $"Install could not be verified for {action.App.Name} after a successful exit code. {verificationFailureMessage}");

            return new InstallResult
            {
                AppId = action.App.Id,
                AppName = action.App.Name,
                Success = false,
                Skipped = false,
                RequiresRestart = restartRequired,
                Message = verificationFailureMessage
            };
        }

        action.App.IsInstalled = true;
        action.App.RequiresRestartHint = restartRequired;
        action.App.StatusBadge = "Installed";

        _loggingService?.LogInfo($"Install succeeded for {action.App.Name}. ExitCode={execution.ExitCode}.");
        return new InstallResult
        {
            AppId = action.App.Id,
            AppName = action.App.Name,
            Success = true,
            Skipped = false,
            RequiresRestart = restartRequired,
            Message = execution.RanElevated
                ? "Install completed after administrator approval."
                : execution.Message
        };
    }

    private async Task<DownloadOutcome> DownloadInstallerAsync(
        InstallAction action,
        string tempRoot,
        CancellationToken cancellationToken)
    {
        var installerUrl = action.InstallDefinition.InstallerUrl;
        if (string.IsNullOrWhiteSpace(installerUrl))
        {
            return DownloadOutcome.Fail("Installer URL is missing.");
        }

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
            using var response = await SharedHttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _loggingService?.LogError(
                    $"Download failed for {action.App.Name}. HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
                return DownloadOutcome.Fail($"Download failed: HTTP {(int)response.StatusCode}.");
            }

            await using var networkStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await networkStream.CopyToAsync(fileStream, cancellationToken);

            _loggingService?.LogInfo($"Downloaded installer for {action.App.Name} to {targetPath}");
            return DownloadOutcome.Ok(targetPath);
        }
        catch (OperationCanceledException)
        {
            _loggingService?.LogWarning($"Download cancelled for {action.App.Name}.");
            return DownloadOutcome.Fail("Download cancelled.");
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Download failed for {action.App.Name}: {ex.Message}");
            return DownloadOutcome.Fail($"Download failed: {ex.Message}");
        }
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
            return await ExecuteElevatedInstallCommandAsync(command, cancellationToken);
        }

        var execution = await ExecuteProcessInstallCommandAsync(command, currentPlatform, cancellationToken);
        if (ShouldRetryElevated(action, command, currentPlatform, execution))
        {
            _loggingService?.LogWarning(
                $"Install for {action.App.Name} failed without elevation (ExitCode={execution.ExitCode}). Retrying with administrator approval.");
            return await ExecuteElevatedInstallCommandAsync(command, cancellationToken);
        }

        return execution;
    }

    private async Task<ExecutionOutcome> ExecuteProcessInstallCommandAsync(
        string command,
        string currentPlatform,
        CancellationToken cancellationToken)
    {
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
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var stdout = (await stdoutTask).Trim();
            var stderr = (await stderrTask).Trim();
            var exitCode = process.ExitCode;

            _loggingService?.LogInfo($"Command completed. ExitCode={exitCode}");
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

            return new ExecutionOutcome(success, exitCode, message, RanElevated: false, ElevationCancelled: false);
        }
        catch (OperationCanceledException)
        {
            _loggingService?.LogWarning("Install command cancelled.");
            return new ExecutionOutcome(false, -1, "Install command cancelled.", RanElevated: false, ElevationCancelled: false);
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Install command execution failed: {ex.Message}");
            return new ExecutionOutcome(false, -1, $"Install command execution failed: {ex.Message}", RanElevated: false, ElevationCancelled: false);
        }
    }

    private async Task<ExecutionOutcome> ExecuteElevatedInstallCommandAsync(
        string command,
        CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = BuildElevatedStartInfo(command);

            _loggingService?.LogInfo($"Executing install command with administrator approval: {command}");
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            var exitCode = process.ExitCode;
            _loggingService?.LogInfo($"Elevated command completed. ExitCode={exitCode}");

            var success = exitCode == 0 || exitCode == 3010 || exitCode == 1641;
            var message = success
                ? "Install command completed successfully."
                : $"Install command failed with exit code {exitCode}.";

            return new ExecutionOutcome(success, exitCode, message, RanElevated: true, ElevationCancelled: false);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            const string message = "Windows permission prompt was cancelled. The install did not continue.";
            _loggingService?.LogWarning(message);
            return new ExecutionOutcome(false, 1223, message, RanElevated: true, ElevationCancelled: true);
        }
        catch (OperationCanceledException)
        {
            _loggingService?.LogWarning("Elevated install command cancelled.");
            return new ExecutionOutcome(false, -1, "Install command cancelled.", RanElevated: true, ElevationCancelled: false);
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Elevated install command execution failed: {ex.Message}");
            return new ExecutionOutcome(false, -1, $"Install command execution failed: {ex.Message}", RanElevated: true, ElevationCancelled: false);
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
                    ? FirstNonEmpty(action.InstallDefinition.SilentArguments, "/qn /norestart")
                    : action.InstallDefinition.Arguments;
                return $"msiexec /i \"{installerPath}\" {msiArgs}".Trim();
            }

            var args = action.UseSilent ? action.InstallDefinition.SilentArguments : action.InstallDefinition.Arguments;
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
            RequiresRestart = false,
            Message = reason
        };
    }

    private static string FirstNonEmpty(string primary, string fallback)
    {
        return string.IsNullOrWhiteSpace(primary) ? fallback : primary;
    }

    private static bool HasCommandFallback(InstallDefinition definition)
    {
        return !string.IsNullOrWhiteSpace(definition.Command) ||
               !string.IsNullOrWhiteSpace(definition.SilentCommand);
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
        return value.Length <= 300 ? value : value[..300] + "...";
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

    private sealed record DownloadOutcome(bool Success, string? FilePath, string Message)
    {
        public static DownloadOutcome Ok(string filePath) => new(true, filePath, "Download succeeded.");

        public static DownloadOutcome Fail(string message) => new(false, null, message);
    }

    private sealed record ExecutionOutcome(
        bool Success,
        int ExitCode,
        string Message,
        bool RanElevated,
        bool ElevationCancelled);
}
