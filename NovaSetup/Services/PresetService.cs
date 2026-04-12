using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class PresetService
{
    private static readonly IReadOnlyList<AppPreset> Presets =
    [
        new AppPreset
        {
            Id = "dev",
            Name = "Developer Setup",
            Description = "Core development tools for coding, terminals, APIs, and vaults.",
            Icon = "DEV",
            AppIds =
            [
                "vscode",
                "git",
                "github-desktop",
                "python",
                "nodejs",
                "docker",
                "windows-terminal",
                "postman",
                "bitwarden",
                "obsidian"
            ]
        },
        new AppPreset
        {
            Id = "gaming",
            Name = "Gaming PC",
            Description = "Launchers, chat, capture, and music for a fresh gaming setup.",
            Icon = "GAME",
            AppIds =
            [
                "steam",
                "discord",
                "epic-games",
                "ubisoft-connect",
                "ea-app",
                "battlenet",
                "obs-studio",
                "spotify"
            ]
        },
        new AppPreset
        {
            Id = "home",
            Name = "Basic Home PC",
            Description = "Daily browsing, media, office, email, and password essentials.",
            Icon = "HOME",
            AppIds =
            [
                "chrome",
                "firefox",
                "vlc",
                "spotify",
                "zoom",
                "bitwarden",
                "libreoffice",
                "7zip",
                "thunderbird",
                "notion"
            ]
        },
        new AppPreset
        {
            Id = "creative",
            Name = "Creative Suite",
            Description = "Image, audio, video, and rendering tools for creative work.",
            Icon = "MEDIA",
            AppIds =
            [
                "gimp",
                "inkscape",
                "blender",
                "krita",
                "davinci-resolve",
                "audacity",
                "obs-studio",
                "handbrake"
            ]
        },
        new AppPreset
        {
            Id = "work",
            Name = "Work From Home",
            Description = "Messaging, meetings, notes, email, and core remote-work apps.",
            Icon = "WORK",
            AppIds =
            [
                "slack",
                "zoom",
                "microsoft-teams",
                "notion",
                "obsidian",
                "bitwarden",
                "vscode",
                "thunderbird"
            ]
        }
    ];

    public IReadOnlyList<AppPreset> GetAllPresets()
    {
        return Presets;
    }

    public AppPreset? GetPreset(string presetId)
    {
        return Presets.FirstOrDefault(preset =>
            string.Equals(preset.Id, presetId, StringComparison.OrdinalIgnoreCase));
    }
}
