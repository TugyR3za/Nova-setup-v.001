using System.Text.Json.Serialization;
using NovaSetup.Services;

namespace NovaSetup.Models;

public sealed class NovaProfile
{
    public string ProfileName { get; set; } = "My Setup";

    public string CreatedBy { get; set; } = string.Empty;

    public string CreatedOn { get; set; } = DateTimeOffset.UtcNow.ToString("O");

    public string NovaVersion { get; set; } = VersionService.GetAppVersion();

    public string Description { get; set; } = string.Empty;

    public List<string> SelectedAppIds { get; set; } = new();

    public string Platform { get; set; } = string.Empty;

    [JsonIgnore]
    public string DisplayCreatedOn =>
        DateTimeOffset.TryParse(CreatedOn, out var createdOn)
            ? createdOn.ToLocalTime().ToString("g")
            : CreatedOn;

    [JsonIgnore]
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    [JsonIgnore]
    public string DisplaySelectedAppsText => $"{SelectedAppIds.Count} app(s)";
}
