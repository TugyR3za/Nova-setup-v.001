using System.Text.Json;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class AppPreferencesService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly LoggingService? _loggingService;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly string _preferencesFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NovaSetup",
        "app-preferences.json");

    public AppPreferencesService(LoggingService? loggingService = null)
    {
        _loggingService = loggingService;
    }

    public async Task SavePreferenceAsync(string appId, AppUserPreference pref)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return;
        }

        await _fileLock.WaitAsync();
        try
        {
            var preferences = await LoadPreferencesCoreAsync();
            preferences[appId] = pref ?? new AppUserPreference();
            await SavePreferencesCoreAsync(preferences);
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Failed to save app preference for '{appId}': {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<AppUserPreference> GetPreferenceAsync(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return new AppUserPreference();
        }

        await _fileLock.WaitAsync();
        try
        {
            var preferences = await LoadPreferencesCoreAsync();
            return preferences.TryGetValue(appId, out var preference)
                ? preference ?? new AppUserPreference()
                : new AppUserPreference();
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Failed to load app preference for '{appId}': {ex.Message}");
            return new AppUserPreference();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task ApplyToAppsAsync(List<AppItem> apps)
    {
        if (apps is null || apps.Count == 0)
        {
            return;
        }

        await _fileLock.WaitAsync();
        try
        {
            var preferences = await LoadPreferencesCoreAsync();
            foreach (var app in apps)
            {
                if (app is null || string.IsNullOrWhiteSpace(app.Id))
                {
                    continue;
                }

                if (!preferences.TryGetValue(app.Id, out var preference) || preference is null)
                {
                    app.UserDisabledSilentInstall = false;
                    app.UserDisabledScanning = false;
                    app.UserDisabledAutoUpdate = false;
                    app.UserTrustedInstallScripts = false;
                    continue;
                }

                app.UserDisabledSilentInstall = preference.DisableSilentInstall;
                app.UserDisabledScanning = preference.DisableScanning;
                app.UserDisabledAutoUpdate = preference.DisableAutoUpdate;
                app.UserTrustedInstallScripts = preference.AllowInstallScripts;
            }
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Failed to apply app preferences: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<Dictionary<string, AppUserPreference>> LoadPreferencesCoreAsync()
    {
        try
        {
            if (!File.Exists(_preferencesFilePath))
            {
                return new Dictionary<string, AppUserPreference>(StringComparer.OrdinalIgnoreCase);
            }

            await using var stream = File.OpenRead(_preferencesFilePath);
            var preferences = await JsonSerializer.DeserializeAsync<Dictionary<string, AppUserPreference>>(stream, JsonOptions);
            return preferences is null
                ? new Dictionary<string, AppUserPreference>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, AppUserPreference>(preferences, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Failed to read app preferences: {ex.Message}");
            return new Dictionary<string, AppUserPreference>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task SavePreferencesCoreAsync(Dictionary<string, AppUserPreference> preferences)
    {
        try
        {
            var directory = Path.GetDirectoryName(_preferencesFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(_preferencesFilePath);
            await JsonSerializer.SerializeAsync(stream, preferences, JsonOptions);
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Failed to write app preferences: {ex.Message}");
        }
    }
}

public sealed class AppUserPreference
{
    public bool DisableSilentInstall { get; set; }

    public bool DisableScanning { get; set; }

    public bool DisableAutoUpdate { get; set; }

    public bool AllowInstallScripts { get; set; }
}
