using System.Text.Json;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class SelectionService
{
    private readonly LoggingService _loggingService;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public SelectionService(LoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    public SelectionConfig? LoadSelection()
    {
        var configPath = ResolveConfigPath("selection.json");
        if (!File.Exists(configPath))
        {
            _loggingService.Info("selection.json not found. Starting with default app state.");
            return null;
        }

        var rawJson = File.ReadAllText(configPath);
        var selection = JsonSerializer.Deserialize<SelectionConfig>(rawJson, _jsonOptions);
        if (selection is null)
        {
            _loggingService.Warn("selection.json exists but could not be parsed.");
            return null;
        }

        selection.SelectedApps ??= new List<string>();
        selection.Settings ??= new SelectionSettings();
        _loggingService.Info($"Loaded selection profile: {selection.ProfileName}");
        return selection;
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
            if (selectedIds.Contains(app.Id))
            {
                app.IsSelected = true;
            }

            app.WillBeSkipped = app.IsSelected && !app.IsSupportedOnCurrentPlatform;
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
        _loggingService.Info($"Saved selection profile to {configPath}");
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
