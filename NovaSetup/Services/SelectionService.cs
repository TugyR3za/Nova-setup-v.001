using System.Text.Json;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class SelectionService
{
    private readonly LoggingService? _loggingService;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public SelectionService(LoggingService? loggingService = null)
    {
        _loggingService = loggingService;
    }

    // Loads Configs/selection.json if present and returns null on missing/invalid data.
    public SelectionConfig? LoadSelection()
    {
        var configPath = ResolveConfigPath("selection.json");
        if (!File.Exists(configPath))
        {
            _loggingService?.Info("selection.json not found.");
            return null;
        }

        try
        {
            var rawJson = File.ReadAllText(configPath);
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                _loggingService?.Warn("selection.json is empty.");
                return null;
            }

            var selection = JsonSerializer.Deserialize<SelectionConfig>(rawJson, _jsonOptions);
            if (selection is null)
            {
                _loggingService?.Warn("selection.json could not be deserialized.");
                return null;
            }

            selection.SelectedApps ??= new List<string>();
            selection.Settings ??= new SelectionSettings();
            ApplyLegacySettingsFallback(selection, rawJson);
            return selection;
        }
        catch (JsonException ex)
        {
            _loggingService?.Warn($"Invalid selection.json format: {ex.Message}");
            return null;
        }
        catch (IOException ex)
        {
            _loggingService?.Warn($"Could not read selection.json: {ex.Message}");
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _loggingService?.Warn($"Access denied reading selection.json: {ex.Message}");
            return null;
        }
    }

    public void ApplySelection(IList<AppItem> catalog, SelectionConfig? selection)
    {
        if (selection is null)
        {
            return;
        }

        var selectedIds = new HashSet<string>(selection.SelectedApps, StringComparer.OrdinalIgnoreCase);
        foreach (var app in catalog)
        {
            if (!selectedIds.Contains(app.Id))
            {
                continue;
            }

            app.IsSelected = true;
            app.WillBeSkipped = !app.IsSupportedOnCurrentPlatform;
            if (app.WillBeSkipped)
            {
                app.StatusBadge = "Will Be Skipped";
            }
        }
    }

    public void SaveSelection(SelectionConfig selection)
    {
        var configPath = ResolveConfigPath("selection.json");
        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(selection, _jsonOptions);
        File.WriteAllText(configPath, json);
        _loggingService?.Info($"Saved selection profile to {configPath}");
    }

    private static void ApplyLegacySettingsFallback(SelectionConfig selection, string rawJson)
    {
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (!TryGetProperty(document.RootElement, "Settings", out var settingsElement) ||
                settingsElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (TryGetBooleanProperty(settingsElement, "OsSupportedApps", out _))
            {
                return;
            }

            if (TryGetBooleanProperty(settingsElement, "ShowUnsupportedApps", out var legacyShowUnsupportedApps))
            {
                selection.Settings.OsSupportedApps = !legacyShowUnsupportedApps;
            }
        }
        catch (JsonException)
        {
            // Ignore fallback parsing if the json payload is malformed.
        }
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetBooleanProperty(JsonElement element, string propertyName, out bool value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                value = property.Value.GetBoolean();
                return true;
            }
        }

        value = default;
        return false;
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
