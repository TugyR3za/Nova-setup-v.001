using System.Text.Json;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class CatalogService
{
    private readonly PlatformService _platformService;
    private readonly LoggingService? _loggingService;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public CatalogService(PlatformService platformService, LoggingService? loggingService = null)
    {
        _platformService = platformService;
        _loggingService = loggingService;
    }

    // Loads Configs/apps.json and safely returns an empty list for missing/invalid input.
    public List<AppItem> LoadApps(string currentPlatform)
    {
        var configPath = ResolveConfigPath("apps.json");
        if (!File.Exists(configPath))
        {
            _loggingService?.Warn($"apps.json not found: {configPath}");
            return new List<AppItem>();
        }

        try
        {
            var rawJson = File.ReadAllText(configPath);
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                _loggingService?.Warn("apps.json is empty.");
                return new List<AppItem>();
            }

            var apps = JsonSerializer.Deserialize<List<AppItem>>(rawJson, _jsonOptions) ?? new List<AppItem>();
            foreach (var app in apps)
            {
                Normalize(app);
                app.IsSupportedOnCurrentPlatform = _platformService.IsSupportedOnPlatform(app.SupportedPlatforms, currentPlatform);
                app.WillBeSkipped = app.IsSelected && !app.IsSupportedOnCurrentPlatform;
                app.RowOpacity = app.IsSupportedOnCurrentPlatform ? 1.0 : 0.56;
                app.StatusBadge = app.IsSupportedOnCurrentPlatform ? "Not Installed" : "Unsupported on this OS";
            }

            _loggingService?.Info($"Loaded {apps.Count} apps from apps.json.");
            return apps;
        }
        catch (JsonException ex)
        {
            _loggingService?.Warn($"Invalid apps.json format: {ex.Message}");
            return new List<AppItem>();
        }
        catch (IOException ex)
        {
            _loggingService?.Warn($"Could not read apps.json: {ex.Message}");
            return new List<AppItem>();
        }
        catch (UnauthorizedAccessException ex)
        {
            _loggingService?.Warn($"Access denied reading apps.json: {ex.Message}");
            return new List<AppItem>();
        }
    }

    public IEnumerable<AppItem> FilterSupportedApps(IEnumerable<AppItem> apps)
    {
        return apps.Where(app => app.IsSupportedOnCurrentPlatform);
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
        app.RecommendationTags ??= new List<string>();
        app.SupportedPlatforms ??= new PlatformSupport();
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
