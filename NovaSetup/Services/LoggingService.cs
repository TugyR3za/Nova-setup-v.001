namespace NovaSetup.Services;

public sealed class LoggingService
{
    private readonly object _syncRoot = new();

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
    }

    public void LogWarning(string message)
    {
        Write("WARN", message);
    }

    public void LogError(string message)
    {
        Write("ERROR", message);
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
