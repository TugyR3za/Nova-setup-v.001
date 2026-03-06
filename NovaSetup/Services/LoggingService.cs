using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace NovaSetup.Services;

public sealed class LoggingService
{
    private readonly object _syncRoot = new();

    public ObservableCollection<string> LatestMessages { get; } = new();

    public string LogFilePath { get; }

    public LoggingService()
    {
        var logsDirectory = ResolveLogsDirectory();
        LogFilePath = Path.Combine(logsDirectory, $"NovaSetup-{DateTime.UtcNow:yyyyMMdd}.log");
        Info("Logging initialized.");
    }

    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Warn(string message)
    {
        Write("WARN", message);
    }

    public void Error(string message)
    {
        Write("ERROR", message);
    }

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";

        lock (_syncRoot)
        {
            File.AppendAllText(LogFilePath, line + Environment.NewLine);
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            PushLatest(line);
            return;
        }

        Dispatcher.UIThread.Post(() => PushLatest(line));
    }

    private void PushLatest(string line)
    {
        LatestMessages.Insert(0, line);
        if (LatestMessages.Count > 150)
        {
            LatestMessages.RemoveAt(LatestMessages.Count - 1);
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
            var localDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            Directory.CreateDirectory(localDirectory);
            return localDirectory;
        }
    }
}
