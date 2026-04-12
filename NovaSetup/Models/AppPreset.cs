namespace NovaSetup.Models;

public sealed class AppPreset
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Icon { get; init; } = string.Empty;

    public IReadOnlyList<string> AppIds { get; init; } = Array.Empty<string>();

    public string AppCountText => $"{AppIds.Count} apps";
}
