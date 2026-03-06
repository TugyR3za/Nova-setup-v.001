using System.Diagnostics;

namespace NovaSetup.Services;

public sealed class BrowserService
{
    private readonly LoggingService? _loggingService;

    public BrowserService(LoggingService? loggingService = null)
    {
        _loggingService = loggingService;
    }

    public bool OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            _loggingService?.LogWarning("Cannot open URL: value is missing or invalid.");
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.ToString(),
                UseShellExecute = true
            });

            _loggingService?.LogInfo($"Opened URL in default browser: {uri}");
            return true;
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Failed to open URL '{uri}': {ex.Message}");
            return false;
        }
    }

    // Convenience alias for app publisher links in the UI.
    public bool OpenPublisherHomepage(string? url) => OpenUrl(url);
}
