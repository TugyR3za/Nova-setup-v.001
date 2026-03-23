namespace NovaSetup.Models;

public sealed class InstallRecord
{
    public int Id { get; set; }

    public string AppId { get; set; } = string.Empty;

    public string AppName { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string InstallMethod { get; set; } = string.Empty;

    public bool Success { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;

    public DateTime InstalledAt { get; set; }

    public long ElapsedMs { get; set; }

    public string StatusText => Success ? "Success" : "Failed";

    public bool Failed => !Success;

    public string DisplayVersion => string.IsNullOrWhiteSpace(Version) ? "-" : Version;

    public string DisplayInstalledAt => InstalledAt.ToLocalTime().ToString("g");

    public string DisplayElapsed => $"{ElapsedMs} ms";
}
