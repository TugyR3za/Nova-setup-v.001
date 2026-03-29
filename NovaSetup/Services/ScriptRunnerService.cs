using System.Diagnostics;
using System.Collections.Generic;
using NovaSetup.Models;

namespace NovaSetup.Services;

public record ScriptResult(bool Success, int ExitCode, string Output, string Error);

public class ScriptRunnerService
{
    private readonly LoggingService? _loggingService;

    public ScriptRunnerService(LoggingService? loggingService = null)
    {
        _loggingService = loggingService;
    }

    public Func<AppSettings?>? SettingsAccessor { get; set; }

    public async Task<ScriptResult> RunScriptAsync(
        string scriptContentOrPath,
        string appId,
        string scriptPhase,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scriptContentOrPath))
        {
            return new ScriptResult(true, 0, string.Empty, string.Empty);
        }

        var settings = SettingsAccessor?.Invoke();
        if (settings is not null && !settings.AllowScriptExecution)
        {
            _loggingService?.LogInfo(
                $"[ScriptRunner] Script execution is disabled in settings. Skipping {scriptPhase} script for {appId}.");
            return new ScriptResult(true, 0, "Skipped - script execution disabled.", string.Empty);
        }

        if (!OperatingSystem.IsWindows())
        {
            _loggingService?.LogWarning(
                $"[ScriptRunner] PowerShell script execution is only supported on Windows. Skipping {scriptPhase} script for {appId}.");
            return new ScriptResult(false, -1, string.Empty, "PowerShell script execution is only supported on Windows.");
        }

        string? scriptPath = null;
        var createdTempFile = false;
        Process? process = null;

        try
        {
            scriptPath = ResolveScriptPath(scriptContentOrPath);
            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                scriptPath = await WriteTempScriptAsync(scriptContentOrPath, appId, scriptPhase, cancellationToken);
                createdTempFile = true;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process = new Process { StartInfo = startInfo };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var output = await stdoutTask;
            var error = await stderrTask;
            var exitCode = process.ExitCode;

            LogScriptOutput(appId, scriptPhase, output, error);

            if (exitCode != 0)
            {
                _loggingService?.LogWarning(
                    $"[ScriptRunner] {scriptPhase} script for {appId} exited with code {exitCode}.");
                return new ScriptResult(false, exitCode, output, error);
            }

            _loggingService?.LogInfo($"[ScriptRunner] {scriptPhase} script completed for {appId}.");
            return new ScriptResult(true, exitCode, output, error);
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            throw;
        }
        catch (Exception ex)
        {
            _loggingService?.LogWarning(
                $"[ScriptRunner] Failed to run {scriptPhase} script for {appId}: {ex.Message}");
            return new ScriptResult(false, -1, string.Empty, ex.Message);
        }
        finally
        {
            process?.Dispose();

            if (createdTempFile && !string.IsNullOrWhiteSpace(scriptPath) && File.Exists(scriptPath))
            {
                try
                {
                    File.Delete(scriptPath);
                }
                catch
                {
                    // Temporary script cleanup failure is non-fatal.
                }
            }
        }
    }

    private static string? ResolveScriptPath(string scriptContentOrPath)
    {
        if (!scriptContentOrPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Path.IsPathRooted(scriptContentOrPath) && File.Exists(scriptContentOrPath))
        {
            return scriptContentOrPath;
        }

        var workingDirectoryPath = Path.GetFullPath(scriptContentOrPath, Directory.GetCurrentDirectory());
        if (File.Exists(workingDirectoryPath))
        {
            return workingDirectoryPath;
        }

        var appBaseDirectoryPath = Path.GetFullPath(scriptContentOrPath, AppContext.BaseDirectory);
        if (File.Exists(appBaseDirectoryPath))
        {
            return appBaseDirectoryPath;
        }

        return null;
    }

    private static async Task<string> WriteTempScriptAsync(
        string scriptContent,
        string appId,
        string scriptPhase,
        CancellationToken cancellationToken)
    {
        var tempFileName = $"NovaSetup-{appId}-{scriptPhase}-{Guid.NewGuid():N}.ps1";
        var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);
        await File.WriteAllTextAsync(tempFilePath, scriptContent, cancellationToken);
        return tempFilePath;
    }

    private void LogScriptOutput(string appId, string scriptPhase, string output, string error)
    {
        var outputParts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(output))
        {
            outputParts.Add(output);
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            outputParts.Add(error);
        }

        var combinedOutput = string.Join(Environment.NewLine, outputParts);

        if (string.IsNullOrWhiteSpace(combinedOutput))
        {
            return;
        }

        var trimmed = combinedOutput.Length <= 500
            ? combinedOutput
            : combinedOutput[..500];
        _loggingService?.LogDebug(
            $"[ScriptRunner] {scriptPhase} script output for {appId}: {trimmed}");
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
                process.Kill(true);
            }
        }
        catch
        {
            // Best-effort cancellation cleanup.
        }
    }
}
