using System.Text.Json;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class CatalogService
{
    private const string RemoteCatalogUrl = "https://raw.githubusercontent.com/TugyR3za/Nova-setup-v.001/main/NovaSetup/Configs/apps.json";
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(8) };

    private readonly PlatformService _platformService;
    private readonly LoggingService? _loggingService;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static string CacheFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NovaSetup",
            "catalog_cache.json");

    internal static HttpClient SharedHttpClient => _httpClient;

    public CatalogService(PlatformService platformService, LoggingService? loggingService = null)
    {
        _platformService = platformService;
        _loggingService = loggingService;
    }

    public async Task<List<AppItem>> LoadAppsAsync(string currentPlatform)
    {
        try
        {
            var remoteJson = await _httpClient.GetStringAsync(RemoteCatalogUrl);
            var remoteApps = ParseAndNormalize(remoteJson, currentPlatform);

            var cacheDirectory = Path.GetDirectoryName(CacheFilePath);
            if (!string.IsNullOrWhiteSpace(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }

            await File.WriteAllTextAsync(CacheFilePath, remoteJson);
            _loggingService?.Info($"Loaded catalog from remote server ({remoteApps.Count} apps).");
            return remoteApps;
        }
        catch (Exception ex)
        {
            _loggingService?.Warn($"Remote catalog unavailable: {ex.Message} — falling back to local cache.");
        }

        if (File.Exists(CacheFilePath))
        {
            try
            {
                var cacheJson = await File.ReadAllTextAsync(CacheFilePath);
                var cachedApps = ParseAndNormalize(cacheJson, currentPlatform);
                _loggingService?.Info("Loaded catalog from local cache.");
                return cachedApps;
            }
            catch (Exception ex)
            {
                _loggingService?.Warn($"Local cache could not be parsed: {ex.Message}");
            }
        }

        var configPath = ResolveConfigPath("apps.json");
        if (!File.Exists(configPath))
        {
            _loggingService?.Warn($"apps.json not found: {configPath}");
            return new List<AppItem>();
        }

        try
        {
            var bundledJson = await File.ReadAllTextAsync(configPath);
            var bundledApps = ParseAndNormalize(bundledJson, currentPlatform);

            if (File.Exists(CacheFilePath))
            {
                _loggingService?.Info("Loaded catalog from bundled fallback.");
            }
            else
            {
                _loggingService?.Info("No cache found — loaded catalog from bundled fallback.");
            }

            return bundledApps;
        }
        catch (Exception ex)
        {
            _loggingService?.Warn($"Bundled apps.json could not be parsed: {ex.Message}");
            return new List<AppItem>();
        }
    }

    public IEnumerable<AppItem> FilterSupportedApps(IEnumerable<AppItem> apps)
    {
        return apps.Where(app => app.IsSupportedOnCurrentPlatform && !app.IsHidden);
    }

    private static void Normalize(AppItem app)
    {
        app.Id ??= string.Empty;
        app.Name ??= string.Empty;
        app.Category = string.IsNullOrWhiteSpace(app.Category) ? "Utilities" : app.Category;
        app.PublisherName = string.IsNullOrWhiteSpace(app.PublisherName) ? "Unknown" : app.PublisherName;
        app.HomepageUrl ??= string.Empty;
        app.Description ??= string.Empty;
        app.IconPath ??= string.Empty;
        app.Version ??= string.Empty;
        app.License ??= string.Empty;
        app.ReleaseNotesUrl ??= string.Empty;
        app.Tags ??= new List<string>();
        app.Dependencies ??= new List<string>();
        app.RecommendationTags ??= new List<string>();
        app.SupportedPlatforms ??= new PlatformSupport();
    }

    private List<AppItem> ParseAndNormalize(string json, string currentPlatform)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new JsonException("Catalog JSON is empty.");
        }

        var apps = JsonSerializer.Deserialize<List<AppItem>>(json, _jsonOptions) ?? new List<AppItem>();
        foreach (var app in apps)
        {
            Normalize(app);
            app.IsSupportedOnCurrentPlatform = _platformService.IsSupportedOnPlatform(app.SupportedPlatforms, currentPlatform);
            app.WillBeSkipped = app.IsSelected && !app.IsSupportedOnCurrentPlatform;
            app.RowOpacity = app.IsSupportedOnCurrentPlatform ? 1.0 : 0.56;
            app.StatusBadge = app.IsSupportedOnCurrentPlatform ? "Not Installed" : "Unsupported on this OS";
        }

        return apps;
    }

    private static string ResolveConfigPath(string fileName)
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "Configs", fileName);
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "Configs", fileName);
    }
}
