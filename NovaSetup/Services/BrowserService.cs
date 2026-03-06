using System.Diagnostics;

namespace NovaSetup.Services;

public sealed class BrowserService
{
    private readonly LoggingService _loggingService;

    public BrowserService(LoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    public bool OpenPublisherHomepage(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            _loggingService.Warn("Publisher homepage URL is empty or invalid.");
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.ToString(),
                UseShellExecute = true
            });

            _loggingService.Info($"Opened publisher homepage: {uri}");
            return true;
        }
        catch (Exception ex)
        {
            _loggingService.Error($"Failed to open browser: {ex.Message}");
            return false;
        }
    }
}
