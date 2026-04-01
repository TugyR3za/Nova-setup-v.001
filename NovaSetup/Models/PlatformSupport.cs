using System.Text.Json.Serialization;

namespace NovaSetup.Models;

public class PlatformSupport
{
    public bool Windows { get; set; }

    public bool Linux { get; set; }

    [JsonPropertyName("macOS")]
    public bool MacOS { get; set; }
}
