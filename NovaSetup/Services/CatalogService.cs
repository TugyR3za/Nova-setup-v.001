using System.Text.Json;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class CatalogService
{
    private readonly PlatformService _platformService;
    private readonly LoggingService _loggingService;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public CatalogService(PlatformService platformService, LoggingService loggingService)
    {
        _platformService = platformService;
        _loggingService = loggingService;
    }

    public List<AppItem> LoadApps(string currentPlatform)
    {
        var configPath = ResolveConfigPath("apps.json");
        if (!File.Exists(configPath))
        {
            _loggingService.Warn($"Catalog file not found: {configPath}");
            return new List<AppItem>();
        }

        var rawJson = File.ReadAllText(configPath);
        var apps = JsonSerializer.Deserialize<List<AppItem>>(rawJson, _jsonOptions) ?? new List<AppItem>();

        foreach (var app in apps)
        {
            Normalize(app);
            app.IsSupportedOnCurrentPlatform = _platformService.IsSupportedOnPlatform(app.SupportedPlatforms, currentPlatform);
            app.WillBeSkipped = app.IsSelected && !app.IsSupportedOnCurrentPlatform;
            app.RowOpacity = app.IsSupportedOnCurrentPlatform ? 1.0 : 0.55;
            app.StatusBadge = app.IsSupportedOnCurrentPlatform ? "Not Installed" : "Unsupported on this OS";
        }

        _loggingService.Info($"Loaded {apps.Count} app entries from apps.json.");
        return apps;
    }

    public IEnumerable<AppItem> FilterSupportedApps(IEnumerable<AppItem> apps)
    {
        return apps.Where(app => app.IsSupportedOnCurrentPlatform);
    }

    private static void Normalize(AppItem app)
    {
        app.Id = app.Id ?? string.Empty;
        app.Name = app.Name ?? string.Empty;
        app.Category = string.IsNullOrWhiteSpace(app.Category) ? "Utilities" : app.Category;
        app.PublisherName = string.IsNullOrWhiteSpace(app.PublisherName) ? "Unknown" : app.PublisherName;
        app.HomepageUrl = app.HomepageUrl ?? string.Empty;
        app.Description = app.Description ?? string.Empty;
        app.IconPath = app.IconPath ?? string.Empty;
        app.RecommendedVendors ??= new List<string>();
        app.SupportedPlatforms ??= new PlatformSupport();
    }

    private static string ResolveConfigPath(string fileName)
    {
        var localPath = Path.Combine(AppContext.BaseDirectory, "Configs", fileName);
        if (File.Exists(localPath))
        {
            return localPath;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "Configs", fileName);
    }
}
