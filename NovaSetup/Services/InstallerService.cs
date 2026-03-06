using System.Diagnostics;
using System.Net.Http;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class InstallerService
{
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();

    private readonly LoggingService? _loggingService;

    public InstallerService(LoggingService? loggingService = null)
    {
        _loggingService = loggingService;
    }

    public async Task<IReadOnlyList<InstallResult>> InstallSelectedAppsAsync(
        IEnumerable<AppItem> selectedApps,
        string currentPlatform,
        bool silentInstallEnabled,
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

        var tempRoot = Path.Combine(
            Path.GetTempPath(),
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
            await CleanupTemporaryFilesAsync(tempRoot, downloadedFiles);
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

        string? installerPath = null;
        if (!string.IsNullOrWhiteSpace(action.InstallDefinition.InstallerUrl))
        {
            var download = await DownloadInstallerAsync(action, tempRoot, cancellationToken);
            if (!download.Success)
            {
                action.App.HasInstallFailed = true;
                return CreateFailureResult(action.App, download.Message);
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

        var execution = await ExecuteInstallCommandAsync(command, currentPlatform, cancellationToken);
        var restartRequired = DetermineIfRestartNeeded(action, execution.ExitCode);

        if (!execution.Success)
        {
            action.App.HasInstallFailed = true;
            _loggingService?.LogError(
                $"Install failed for {action.App.Name}. ExitCode={execution.ExitCode}. {execution.Message}");

            return new InstallResult
            {
                AppId = action.App.Id,
                AppName = action.App.Name,
                Success = false,
                Skipped = false,
                RequiresRestart = restartRequired,
                Message = execution.Message
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
            Message = execution.Message
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

            return new ExecutionOutcome(success, exitCode, message);
        }
        catch (OperationCanceledException)
        {
            _loggingService?.LogWarning("Install command cancelled.");
            return new ExecutionOutcome(false, -1, "Install command cancelled.");
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Install command execution failed: {ex.Message}");
            return new ExecutionOutcome(false, -1, $"Install command execution failed: {ex.Message}");
        }
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

        if (action.UseSilent && !string.IsNullOrWhiteSpace(action.InstallDefinition.SilentCommand))
        {
            return action.InstallDefinition.SilentCommand;
        }

        return action.InstallDefinition.Command;
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

    private static string TrimForLog(string value)
    {
        return value.Length <= 300 ? value : value[..300] + "...";
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

    private sealed record ExecutionOutcome(bool Success, int ExitCode, string Message);
}
