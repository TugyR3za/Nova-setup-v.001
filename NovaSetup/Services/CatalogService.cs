using System.Text.Json;
using System.Text.RegularExpressions;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class CatalogService
{
    private const string RemoteCatalogUrl = AppConstants.RemoteCatalogUrl;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly Regex WingetIdRegex =
        new(@"--id\s+(?:""(?<id>[^""]+)""|(?<id>\S+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly PlatformService _platformService;
    private readonly LoggingService? _loggingService;
    private Task? _backgroundRefreshTask;
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

    public CatalogService(LoggingService? loggingService = null)
        : this(new PlatformService(), loggingService)
    {
    }

    public CatalogService(PlatformService platformService, LoggingService? loggingService = null)
    {
        _platformService = platformService;
        _loggingService = loggingService;
    }

    public async Task<List<AppItem>> LoadAppsAsync(string currentPlatform)
    {
        var configPath = ResolveConfigPath("apps.json");
        var cacheCatalog = await TryLoadCatalogFromFileAsync(CacheFilePath, currentPlatform, "local cache");
        var bundledCatalog = await TryLoadCatalogFromFileAsync(configPath, currentPlatform, "bundled fallback");

        List<AppItem>? localApps = null;
        string? localJson = null;
        string? selectedSource = null;

        if (bundledCatalog.Apps is not null && cacheCatalog.Apps is not null)
        {
            var bundledWriteTime = GetFileWriteTimeUtc(configPath);
            var cacheWriteTime = GetFileWriteTimeUtc(CacheFilePath);
            var useBundledCatalog =
                bundledWriteTime >= cacheWriteTime ||
                bundledCatalog.Apps.Count > cacheCatalog.Apps.Count;

            if (useBundledCatalog)
            {
                localApps = bundledCatalog.Apps;
                localJson = bundledCatalog.Json;
                selectedSource = "bundled fallback";
            }
            else
            {
                localApps = cacheCatalog.Apps;
                localJson = cacheCatalog.Json;
                selectedSource = "local cache";
            }
        }
        else if (bundledCatalog.Apps is not null)
        {
            localApps = bundledCatalog.Apps;
            localJson = bundledCatalog.Json;
            selectedSource = "bundled fallback";
        }
        else if (cacheCatalog.Apps is not null)
        {
            localApps = cacheCatalog.Apps;
            localJson = cacheCatalog.Json;
            selectedSource = "local cache";
        }

        if (localApps is not null && !string.IsNullOrWhiteSpace(selectedSource))
        {
            _loggingService?.Info($"Loaded catalog from {selectedSource} ({localApps.Count} apps).");

            if (string.Equals(selectedSource, "bundled fallback", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(localJson))
            {
                await TryWriteCacheAsync(localJson);
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
            // Tracked background refresh with its own timeout so it can't hang indefinitely
            if (_backgroundRefreshTask is null || _backgroundRefreshTask.IsCompleted)
            {
                _backgroundRefreshTask = Task.Run(async () =>
                {
                    try
                    {
                        using var refreshCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                        var remoteJson = await _httpClient.GetStringAsync(RemoteCatalogUrl, refreshCts.Token);
                        var cacheDirectory = Path.GetDirectoryName(CacheFilePath);
                        if (!string.IsNullOrWhiteSpace(cacheDirectory))
                            Directory.CreateDirectory(cacheDirectory);
                        await File.WriteAllTextAsync(CacheFilePath, remoteJson, refreshCts.Token);
                        _loggingService?.Info("Background catalog refresh completed.");
                    }
                    catch (OperationCanceledException)
                    {
                        _loggingService?.Warn("Background catalog refresh timed out.");
                    }
                    catch (Exception ex)
                    {
                        _loggingService?.Warn($"Background catalog refresh failed: {ex.Message}");
                    }
                });
            }
        }

        return localApps;
    }

    private async Task<(List<AppItem>? Apps, string? Json)> TryLoadCatalogFromFileAsync(
        string path,
        string currentPlatform,
        string sourceName)
    {
        if (!File.Exists(path))
        {
            return (null, null);
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return (ParseAndNormalize(json, currentPlatform), json);
        }
        catch (Exception ex)
        {
            _loggingService?.Warn($"{sourceName} could not be parsed: {ex.Message}");
            return (null, null);
        }
    }

    private static DateTime GetFileWriteTimeUtc(string path)
    {
        try
        {
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static async Task TryWriteCacheAsync(string json)
    {
        try
        {
            var cacheDirectory = Path.GetDirectoryName(CacheFilePath);
            if (!string.IsNullOrWhiteSpace(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }

            await File.WriteAllTextAsync(CacheFilePath, json);
        }
        catch
        {
            // Cache sync is best-effort only.
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
        app.LogoUrl = string.IsNullOrWhiteSpace(app.LogoUrl) ? null : app.LogoUrl;
        app.WingetId ??= string.Empty;
        app.Version ??= string.Empty;
        app.License ??= string.Empty;
        app.ReleaseNotesUrl ??= string.Empty;
        app.Tags ??= new List<string>();
        app.Dependencies ??= new List<string>();
        app.RecommendationTags ??= new List<string>();
        app.SupportedPlatforms ??= new PlatformSupport();
        NormalizeInstallDefinition(app.WindowsInstall);
        NormalizeInstallDefinition(app.LinuxInstall);
        NormalizeInstallDefinition(app.MacOSInstall);

        if (string.IsNullOrWhiteSpace(app.WingetId))
        {
            app.WingetId = ExtractWingetId(app.WindowsInstall);
        }
    }

    private static void NormalizeInstallDefinition(InstallDefinition? installDefinition)
    {
        if (installDefinition is null)
        {
            return;
        }

        installDefinition.InstallerUrl ??= string.Empty;
        installDefinition.InstallerUrl32 ??= string.Empty;
        installDefinition.InstallerUrl64 ??= string.Empty;
        installDefinition.InstallerUrlArm64 ??= string.Empty;
        installDefinition.InstallerFileName ??= string.Empty;
        installDefinition.Sha256 ??= string.Empty;
        installDefinition.Sha25632 ??= string.Empty;
        installDefinition.Sha25664 ??= string.Empty;
        installDefinition.Command ??= string.Empty;
        installDefinition.SilentCommand ??= string.Empty;
        installDefinition.Arguments ??= string.Empty;
        installDefinition.SilentArguments ??= string.Empty;
        installDefinition.SilentArgumentsArm64 ??= string.Empty;
        installDefinition.Architecture ??= string.Empty;
        installDefinition.PortableArchiveUrl ??= string.Empty;
        installDefinition.PortableExecutable ??= string.Empty;
        installDefinition.PortableArchiveType = string.IsNullOrWhiteSpace(installDefinition.PortableArchiveType)
            ? "zip"
            : installDefinition.PortableArchiveType;
        installDefinition.PortableSubfolder ??= string.Empty;
        installDefinition.VirusTotalUrl ??= string.Empty;
        installDefinition.VirusTotalRatio ??= string.Empty;
        installDefinition.PreInstallScript ??= string.Empty;
        installDefinition.PostInstallScript ??= string.Empty;
        installDefinition.DetectDisplayNameContains ??= string.Empty;
        installDefinition.DetectExecutable ??= string.Empty;
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
            app.StatusBadge = app.IsSupportedOnCurrentPlatform
                ? AppItem.StatusNotInstalled
                : AppItem.StatusUnsupportedOnCurrentOs;
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
