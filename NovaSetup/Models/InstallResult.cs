namespace NovaSetup.Models;

public class InstallResult
{
    public string AppId { get; set; } = string.Empty;

    public string AppName { get; set; } = string.Empty;

    public bool Success { get; set; }

    public bool Skipped { get; set; }

    public bool RequiresRestart { get; set; }

    public string Message { get; set; } = string.Empty;
}
