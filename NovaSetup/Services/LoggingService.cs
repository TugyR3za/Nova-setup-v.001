using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace NovaSetup.Services;

public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error,
    Debug
}

public sealed class LogEntry
{
    public DateTime Timestamp { get; init; }

    public string Message { get; init; } = string.Empty;

    public LogLevel Level { get; init; }

    public string FormattedTime => Timestamp.ToString("HH:mm:ss");

    public string DisplayText => $"[{FormattedTime}] {Message}";
}

public sealed class LoggingService
{
    private const int MaxLiveEntries = 500;
    private readonly object _syncRoot = new();

    public static readonly ObservableCollection<LogEntry> LiveLogs = new();

    public static Func<bool>? DeveloperModeAccessor { get; set; }

    public string LogFilePath { get; }

    public LoggingService()
    {
        var logsDirectory = ResolveLogsDirectory();
        LogFilePath = Path.Combine(logsDirectory, $"NovaSetup-{DateTime.Now:yyyyMMdd}.log");
        LogInfo("Logging initialized.");
    }

    public void LogInfo(string message)
    {
        Write("INFO", message);
        AppendLiveLog(LogLevel.Info, message);
    }

    public void LogSuccess(string message)
    {
        Write("SUCCESS", message);
        AppendLiveLog(LogLevel.Success, message);
    }

    public void LogWarning(string message)
    {
        Write("WARN", message);
        AppendLiveLog(LogLevel.Warning, message);
    }

    public void LogError(string message)
    {
        Write("ERROR", message);
        AppendLiveLog(LogLevel.Error, message);
    }

    public void LogDebug(string message)
    {
        if (!IsDeveloperModeEnabled())
        {
            return;
        }

        AppendLiveLog(LogLevel.Debug, message);
    }

    // Compatibility helpers for existing services.
    public void Info(string message) => LogInfo(message);

    public void Warn(string message) => LogWarning(message);

    public void Error(string message) => LogError(message);

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
        lock (_syncRoot)
        {
            File.AppendAllText(LogFilePath, line + Environment.NewLine);
        }
    }

    private static void AppendLiveLog(LogLevel level, string message)
    {
        if (level == LogLevel.Debug && !IsDeveloperModeEnabled())
        {
            return;
        }

        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Message = message ?? string.Empty,
            Level = level
        };

        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            LiveLogs.Add(entry);
            while (LiveLogs.Count > MaxLiveEntries)
            {
                LiveLogs.RemoveAt(0);
            }
        });
    }

    private static bool IsDeveloperModeEnabled()
    {
        try
        {
            return DeveloperModeAccessor?.Invoke() == true;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveLogsDirectory()
    {
        var outputDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
        try
        {
            Directory.CreateDirectory(outputDirectory);
            return outputDirectory;
        }
        catch
        {
            var workingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            Directory.CreateDirectory(workingDirectory);
            return workingDirectory;
        }
    }
}
