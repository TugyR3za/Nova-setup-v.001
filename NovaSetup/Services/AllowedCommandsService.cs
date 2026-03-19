namespace NovaSetup.Services;

public sealed class AllowedCommandsService
{
    private readonly HashSet<string> _inProgress = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public static readonly AllowedCommandsService Instance = new();

    private AllowedCommandsService()
    {
    }

    public bool TryBeginInstall(string appId)
    {
        lock (_lock)
        {
            if (_inProgress.Contains(appId))
            {
                return false;
            }

            _inProgress.Add(appId);
            return true;
        }
    }

    public void EndInstall(string appId)
    {
        lock (_lock)
        {
            _inProgress.Remove(appId);
        }
    }

    public bool IsInstalling(string appId)
    {
        lock (_lock)
        {
            return _inProgress.Contains(appId);
        }
    }

    public int ActiveCount
    {
        get
        {
            lock (_lock)
            {
                return _inProgress.Count;
            }
        }
    }
}
