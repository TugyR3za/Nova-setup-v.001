using System.Text.Json;
using System.Text.RegularExpressions;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class CatalogService
{
    private const string RemoteCatalogUrl = "https://raw.githubusercontent.com/TugyR3za/Nova-setup-v.001/main/NovaSetup/Configs/apps.json";
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly Regex WingetIdRegex =
        new(@"--id\s+(?:""(?<id>[^""]+)""|(?<id>\S+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
        // Try local cache first for instant startup
        List<AppItem>? localApps = null;

        if (File.Exists(CacheFilePath))
        {
            try
            {
                var cacheJson = await File.ReadAllTextAsync(CacheFilePath);
                localApps = ParseAndNormalize(cacheJson, currentPlatform);
                _loggingService?.Info($"Loaded catalog from local cache ({localApps.Count} apps).");
            }
            catch (Exception ex)
            {
                _loggingService?.Warn($"Local cache could not be parsed: {ex.Message}");
            }
        }

        if (localApps is null)
        {
            // No cache — try bundled fallback
            var configPath = ResolveConfigPath("apps.json");
            if (File.Exists(configPath))
            {
                try
                {
                    var bundledJson = await File.ReadAllTextAsync(configPath);
                    localApps = ParseAndNormalize(bundledJson, currentPlatform);
                    _loggingService?.Info($"Loaded catalog from bundled fallback ({localApps.Count} apps).");
                }
                catch (Exception ex)
                {
                    _loggingService?.Warn($"Bundled apps.json could not be parsed: {ex.Message}");
                }
            }
        }

        if (localApps is null)
        {
            // No local source at all — must fetch remote synchronously
            try
            {
                var remoteJson = await _httpClient.GetStringAsync(RemoteCatalogUrl);
                localApps = ParseAndNormalize(remoteJson, currentPlatform);

                var cacheDirectory = Path.GetDirectoryName(CacheFilePath);
                if (!string.IsNullOrWhiteSpace(cacheDirectory))
                    Directory.CreateDirectory(cacheDirectory);
                await File.WriteAllTextAsync(CacheFilePath, remoteJson);

                _loggingService?.Info($"Loaded catalog from remote server ({localApps.Count} apps).");
            }
            catch (Exception ex)
            {
                _loggingService?.Warn($"Remote catalog also unavailable: {ex.Message}");
                return new List<AppItem>();
            }
        }
        else
        {
            // Fire-and-forget background refresh from remote
            _ = Task.Run(async () =>
            {
                try
                {
                    var remoteJson = await _httpClient.GetStringAsync(RemoteCatalogUrl);
                    var cacheDirectory = Path.GetDirectoryName(CacheFilePath);
                    if (!string.IsNullOrWhiteSpace(cacheDirectory))
                        Directory.CreateDirectory(cacheDirectory);
                    await File.WriteAllTextAsync(CacheFilePath, remoteJson);
                    _loggingService?.Info("Background catalog refresh completed.");
                }
                catch (Exception ex)
                {
                    _loggingService?.Warn($"Background catalog refresh failed: {ex.Message}");
                }
            });
        }

        return localApps;
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
        app.WingetId ??= string.Empty;
        app.Version ??= string.Empty;
        app.License ??= string.Empty;
        app.ReleaseNotesUrl ??= string.Empty;
        app.Tags ??= new List<string>();
        app.Dependencies ??= new List<string>();
        app.RecommendationTags ??= new List<string>();
        app.SupportedPlatforms ??= new PlatformSupport();

        if (string.IsNullOrWhiteSpace(app.WingetId))
        {
            app.WingetId = ExtractWingetId(app.WindowsInstall);
        }
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

    private static string ExtractWingetId(InstallDefinition? installDefinition)
    {
        var command = string.IsNullOrWhiteSpace(installDefinition?.SilentCommand)
            ? installDefinition?.Command ?? string.Empty
            : installDefinition.SilentCommand;

        if (string.IsNullOrWhiteSpace(command) ||
            command.IndexOf("winget", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return string.Empty;
        }

        var match = WingetIdRegex.Match(command);
        return match.Success ? match.Groups["id"].Value.Trim() : string.Empty;
    }
}
