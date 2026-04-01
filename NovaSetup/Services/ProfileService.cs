using System.Text.Json;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class ProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly LoggingService? _loggingService;

    public ProfileService(SettingsService settingsService, LoggingService? loggingService = null)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        _loggingService = loggingService;
    }

    public async Task SaveProfileAsync(string profileName, List<string> selectedAppIds, string description = "")
    {
        try
        {
            var filePath = GetProfileFilePath(profileName);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var profile = CreateProfile(profileName, selectedAppIds, description);
            await SaveProfileToPathAsync(profile, filePath);
            _loggingService?.LogInfo($"Saved profile '{profile.ProfileName}' to {filePath}");
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Failed to save profile '{profileName}': {ex.Message}");
        }
    }

    public async Task<NovaProfile?> LoadProfileAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                _loggingService?.LogWarning($"Profile file not found: {filePath}");
                return null;
            }

            await using var stream = File.OpenRead(filePath);
            var profile = await JsonSerializer.DeserializeAsync<NovaProfile>(stream, JsonOptions);
            if (profile is null)
            {
                _loggingService?.LogWarning($"Profile file '{filePath}' could not be deserialized.");
                return null;
            }

            NormalizeProfile(profile, Path.GetFileNameWithoutExtension(filePath));
            return profile;
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Failed to load profile '{filePath}': {ex.Message}");
            return null;
        }
    }

    public async Task<List<NovaProfile>> GetSavedProfilesAsync()
    {
        var profiles = new List<NovaProfile>();

        try
        {
            Directory.CreateDirectory(ProfilesDirectory);
            foreach (var filePath in Directory.EnumerateFiles(ProfilesDirectory, "*.nova", SearchOption.TopDirectoryOnly))
            {
                var profile = await LoadProfileAsync(filePath);
                if (profile is not null)
                {
                    profiles.Add(profile);
                }
            }
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Failed to enumerate saved profiles: {ex.Message}");
        }

        return profiles;
    }

    public async Task DeleteProfileAsync(string profileName)
    {
        try
        {
            var filePath = GetProfileFilePath(profileName);
            if (!File.Exists(filePath))
            {
                return;
            }

            await Task.Run(() => File.Delete(filePath));
            _loggingService?.LogInfo($"Deleted profile '{NormalizeProfileName(profileName)}' from {filePath}");
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Failed to delete profile '{profileName}': {ex.Message}");
        }
    }

    public async Task ExportProfileAsync(string profileName, List<string> selectedAppIds, string exportPath, string description = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(exportPath))
            {
                _loggingService?.LogWarning("Profile export skipped because no export path was provided.");
                return;
            }

            var normalizedExportPath = EnsureNovaExtension(exportPath);
            var directory = Path.GetDirectoryName(normalizedExportPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var profile = CreateProfile(profileName, selectedAppIds, description);
            await SaveProfileToPathAsync(profile, normalizedExportPath);
            _loggingService?.LogInfo($"Exported profile '{profile.ProfileName}' to {normalizedExportPath}");
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Failed to export profile '{profileName}': {ex.Message}");
        }
    }

    private static string ProfilesDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaSetup", "Profiles");

    private NovaProfile CreateProfile(string profileName, IEnumerable<string>? selectedAppIds, string description)
    {
        var normalizedName = NormalizeProfileName(profileName);
        return new NovaProfile
        {
            ProfileName = normalizedName,
            CreatedOn = DateTimeOffset.UtcNow.ToString("O"),
            NovaVersion = VersionService.GetAppVersion(),
            Description = description?.Trim() ?? string.Empty,
            SelectedAppIds = selectedAppIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>(),
            Platform = GetCurrentPlatform()
        };
    }

    private static void NormalizeProfile(NovaProfile profile, string fallbackProfileName)
    {
        profile.ProfileName = NormalizeProfileName(string.IsNullOrWhiteSpace(profile.ProfileName) ? fallbackProfileName : profile.ProfileName);
        profile.CreatedBy ??= string.Empty;
        profile.CreatedOn ??= string.Empty;
        profile.NovaVersion ??= string.Empty;
        profile.Description ??= string.Empty;
        profile.SelectedAppIds ??= new List<string>();
        profile.Platform ??= string.Empty;
    }

    private static string NormalizeProfileName(string? profileName)
    {
        var value = string.IsNullOrWhiteSpace(profileName) ? "My Setup" : profileName.Trim();
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidCharacter, '_');
        }

        // Prevent path traversal: strip directory separators and collapse dot sequences
        value = value
            .Replace("..", "_", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .Replace("\\", "_", StringComparison.Ordinal);

        return string.IsNullOrWhiteSpace(value) ? "My Setup" : value;
    }

    private static string GetCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return "windows";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macos";
        }

        if (OperatingSystem.IsLinux())
        {
            return "linux";
        }

        return string.Empty;
    }

    private static string EnsureNovaExtension(string filePath)
    {
        return string.Equals(Path.GetExtension(filePath), ".nova", StringComparison.OrdinalIgnoreCase)
            ? filePath
            : $"{filePath}.nova";
    }

    private static string GetProfileFilePath(string profileName)
    {
        var normalized = NormalizeProfileName(profileName);
        var candidate = Path.GetFullPath(Path.Combine(ProfilesDirectory, $"{normalized}.nova"));

        // Final safety check: ensure the resolved path is still inside our profiles directory
        if (!candidate.StartsWith(Path.GetFullPath(ProfilesDirectory), StringComparison.OrdinalIgnoreCase))
        {
            candidate = Path.Combine(ProfilesDirectory, "My Setup.nova");
        }

        return candidate;
    }

    private static async Task SaveProfileToPathAsync(NovaProfile profile, string filePath)
    {
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, profile, JsonOptions);
        await stream.FlushAsync();
    }
}
