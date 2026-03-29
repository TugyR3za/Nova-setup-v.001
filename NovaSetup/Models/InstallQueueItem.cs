namespace NovaSetup.Models;

public class InstallQueueItem : ObservableObject
{
    private string _appId = string.Empty;
    private string _appName = string.Empty;
    private string _iconGlyph = string.Empty;
    private InstallQueueStatus _status = InstallQueueStatus.Pending;
    private string _statusText = "Waiting...";
    private double _progress;
    private bool _isActive;

    public string AppId
    {
        get => _appId;
        set => SetProperty(ref _appId, value ?? string.Empty);
    }

    public string AppName
    {
        get => _appName;
        set => SetProperty(ref _appName, value ?? string.Empty);
    }

    public string IconGlyph
    {
        get => _iconGlyph;
        set => SetProperty(ref _iconGlyph, value ?? string.Empty);
    }

    public InstallQueueStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value ?? string.Empty);
    }

    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }
}

public enum InstallQueueStatus
{
    Pending,
    Downloading,
    Installing,
    Done,
    Failed,
    Skipped,
    Cancelled
}

public readonly record struct InstallQueueProgress(
    string AppId,
    InstallQueueStatus Status,
    string StatusText,
    double Progress);
