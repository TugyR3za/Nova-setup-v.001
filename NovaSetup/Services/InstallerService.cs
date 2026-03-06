using System.Diagnostics;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class InstallerService
{
    private readonly LoggingService _loggingService;

    public InstallerService(LoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    public async Task<IReadOnlyList<InstallResult>> InstallSelectedAppsAsync(
        IEnumerable<AppItem> apps,
        string currentPlatform,
        bool silentInstallEnabled,
        string defaultInstallLocation,
        CancellationToken cancellationToken = default)
    {
        var results = new List<InstallResult>();

        foreach (var app in apps.Where(candidate => candidate.IsSelected))
        {
            cancellationToken.ThrowIfCancellationRequested();
            app.HasInstallFailed = false;

            if (!app.IsSupportedOnCurrentPlatform)
            {
                results.Add(new InstallResult
                {
                    AppId = app.Id,
                    AppName = app.Name,
                    Success = false,
                    Skipped = true,
                    RequiresRestart = false,
                    Message = "Unsupported on this OS. Skipped."
                });

                _loggingService.Warn($"Skipped unsupported app: {app.Name}");
                continue;
            }

            var installDefinition = currentPlatform == PlatformService.Windows ? app.WindowsInstall : app.LinuxInstall;
            if (installDefinition is null)
            {
                app.HasInstallFailed = true;
                results.Add(new InstallResult
                {
                    AppId = app.Id,
                    AppName = app.Name,
                    Success = false,
                    Skipped = false,
                    RequiresRestart = false,
                    Message = "No install metadata found for this OS."
                });

                _loggingService.Error($"Install metadata missing for app: {app.Name}");
                continue;
            }

            if (installDefinition.NeedsManualInstall)
            {
                results.Add(new InstallResult
                {
                    AppId = app.Id,
                    AppName = app.Name,
                    Success = false,
                    Skipped = true,
                    RequiresRestart = false,
                    Message = "Needs manual install."
                });

                _loggingService.Warn($"Manual install required for app: {app.Name}");
                continue;
            }

            var command = SelectInstallCommand(app, installDefinition, silentInstallEnabled, defaultInstallLocation);
            if (string.IsNullOrWhiteSpace(command))
            {
                app.HasInstallFailed = true;
                results.Add(new InstallResult
                {
                    AppId = app.Id,
                    AppName = app.Name,
                    Success = false,
                    Skipped = false,
                    RequiresRestart = false,
                    Message = "No install command configured."
                });

                _loggingService.Error($"Install command missing for app: {app.Name}");
                continue;
            }

            _loggingService.Info($"Installing {app.Name} with command: {command}");
            var execution = await ExecuteCommandAsync(command, currentPlatform, cancellationToken);
            if (execution.Success)
            {
                app.IsInstalled = true;
                app.RequiresRestartHint = installDefinition.RequiresRestart || app.RequiresRestartHint;
                results.Add(new InstallResult
                {
                    AppId = app.Id,
                    AppName = app.Name,
                    Success = true,
                    Skipped = false,
                    RequiresRestart = app.RequiresRestartHint,
                    Message = "Installed successfully."
                });

                _loggingService.Info($"Install succeeded for app: {app.Name}");
            }
            else
            {
                app.HasInstallFailed = true;
                results.Add(new InstallResult
                {
                    AppId = app.Id,
                    AppName = app.Name,
                    Success = false,
                    Skipped = false,
                    RequiresRestart = false,
                    Message = string.IsNullOrWhiteSpace(execution.Message)
                        ? "Install failed."
                        : execution.Message
                });

                _loggingService.Error($"Install failed for app: {app.Name}. {execution.Message}");
            }
        }

        return results;
    }

    private static string SelectInstallCommand(
        AppItem app,
        InstallDefinition installDefinition,
        bool silentInstallEnabled,
        string defaultInstallLocation)
    {
        var selectedCommand = silentInstallEnabled &&
                              app.SupportsSilentInstall &&
                              !string.IsNullOrWhiteSpace(installDefinition.SilentCommand)
            ? installDefinition.SilentCommand
            : installDefinition.Command;

        if (app.SupportsCustomPath && !string.IsNullOrWhiteSpace(defaultInstallLocation))
        {
            selectedCommand = selectedCommand.Replace("{installPath}", defaultInstallLocation, StringComparison.OrdinalIgnoreCase);
        }

        return selectedCommand;
    }

    private static async Task<(bool Success, string Message)> ExecuteCommandAsync(
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

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode == 0)
            {
                return (true, string.IsNullOrWhiteSpace(output) ? "OK" : output.Trim());
            }

            var combinedError = string.IsNullOrWhiteSpace(error) ? output : error;
            return (false, combinedError.Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
