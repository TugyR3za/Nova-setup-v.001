using System.Text.Json;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class SettingsService
{
    private readonly LoggingService? _loggingService;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public SettingsService(LoggingService? loggingService = null)
    {
        _loggingService = loggingService;
    }

    public bool LoadedDefaultsOnLastLoad { get; private set; }

    public async Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        LoadedDefaultsOnLastLoad = false;
        var configPath = ResolveConfigPath("settings.json");

        if (!File.Exists(configPath))
        {
            var defaults = AppSettings.CreateDefault();
            await SaveSettingsAsync(defaults, cancellationToken);
            LoadedDefaultsOnLastLoad = true;
            _loggingService?.LogInfo($"settings.json not found. Created defaults at {configPath}");
            return defaults;
        }

        try
        {
            await using var stream = File.OpenRead(configPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _jsonOptions, cancellationToken);
            if (settings is null)
            {
                _loggingService?.LogWarning("settings.json was empty or invalid. Falling back to defaults.");
                return await RecoverWithDefaultsAsync(configPath, cancellationToken);
            }

            _loggingService?.LogInfo($"Loaded settings from {configPath}");
            return settings;
        }
        catch (JsonException ex)
        {
            _loggingService?.LogWarning($"Invalid settings.json format: {ex.Message}. Falling back to defaults.");
            return await RecoverWithDefaultsAsync(configPath, cancellationToken);
        }
        catch (IOException ex)
        {
            _loggingService?.LogWarning($"Could not read settings.json: {ex.Message}. Falling back to defaults.");
            return await RecoverWithDefaultsAsync(configPath, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            _loggingService?.LogWarning($"Access denied reading settings.json: {ex.Message}. Falling back to defaults.");
            return await RecoverWithDefaultsAsync(configPath, cancellationToken);
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var configPath = ResolveConfigPath("settings.json");
        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(configPath);
        await JsonSerializer.SerializeAsync(stream, settings?.Clone() ?? AppSettings.CreateDefault(), _jsonOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        _loggingService?.LogInfo($"Saved settings to {configPath}");
    }

    public async Task<AppSettings> ResetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var defaults = AppSettings.CreateDefault();
        await SaveSettingsAsync(defaults, cancellationToken);
        LoadedDefaultsOnLastLoad = true;
        _loggingService?.LogInfo("Settings reset to defaults.");
        return defaults;
    }

    private async Task<AppSettings> RecoverWithDefaultsAsync(string configPath, CancellationToken cancellationToken)
    {
        var defaults = AppSettings.CreateDefault();
        LoadedDefaultsOnLastLoad = true;
        await SaveSettingsAsync(defaults, cancellationToken);
        _loggingService?.LogInfo($"Default settings written to {configPath}");
        return defaults;
    }

    private static string ResolveConfigPath(string fileName)
    {
        var outputDirectory = Path.Combine(AppContext.BaseDirectory, "Configs");
        if (Directory.Exists(outputDirectory))
        {
            return Path.Combine(outputDirectory, fileName);
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "Configs", fileName);
    }
}
