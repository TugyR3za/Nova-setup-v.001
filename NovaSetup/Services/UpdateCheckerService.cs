using System.Text.Json;
using System.Net.Http.Headers;

namespace NovaSetup.Services;

public sealed class UpdateCheckerService
{
    private const string VersionUrl = "https://raw.githubusercontent.com/TugyR3za/Nova-setup-v.001/main/version.json";

    private readonly LoggingService? _loggingService;

    private sealed record RemoteVersionInfo
    {
        public string Version { get; init; } = string.Empty;
        public string ReleaseNotesUrl { get; init; } = string.Empty;
        public string DownloadUrl { get; init; } = string.Empty;
    }

    public UpdateCheckerService(LoggingService? loggingService = null)
    {
        _loggingService = loggingService;
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync()
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            var requestUrl = $"{VersionUrl}?ts={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true
            };

            using var response = await CatalogService.SharedHttpClient.SendAsync(request, timeoutCts.Token);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            var remoteInfo = JsonSerializer.Deserialize<RemoteVersionInfo>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (remoteInfo is null || string.IsNullOrWhiteSpace(remoteInfo.Version))
            {
                throw new JsonException("Remote version payload is missing the version field.");
            }

            var currentVersionText = VersionService.GetAppVersion();
            var currentVersion = Version.Parse(currentVersionText);
            var latestVersion = Version.Parse(remoteInfo.Version.Trim());

            if (latestVersion > currentVersion)
            {
                _loggingService?.LogInfo($"[UpdateChecker] Update available: v{latestVersion} (current: v{currentVersion})");
                return new UpdateCheckResult(
                    true,
                    remoteInfo.Version.Trim(),
                    remoteInfo.DownloadUrl ?? string.Empty,
                    remoteInfo.ReleaseNotesUrl ?? string.Empty);
            }

            _loggingService?.LogInfo($"[UpdateChecker] Nova is up to date (v{currentVersion})");
            return new UpdateCheckResult(
                false,
                remoteInfo.Version.Trim(),
                remoteInfo.DownloadUrl ?? string.Empty,
                remoteInfo.ReleaseNotesUrl ?? string.Empty);
        }
        catch (Exception ex)
        {
            _loggingService?.LogWarning($"[UpdateChecker] Update check failed: {ex.Message}");
            return new UpdateCheckResult(false, string.Empty, string.Empty, string.Empty);
        }
    }
}

public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    string LatestVersion,
    string DownloadUrl,
    string ReleaseNotesUrl);
