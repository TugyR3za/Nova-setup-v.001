using System.Collections.Generic;

namespace NovaSetup.Models;

public class SelectionConfig
{
    public string ProfileName { get; set; } = "Website Package";

    public string TargetPlatform { get; set; } = string.Empty;

    public List<string> SelectedApps { get; set; } = new();

    public SelectionSettings Settings { get; set; } = new();
}

public class SelectionSettings
{
    public bool SilentInstall { get; set; } = true;

    public bool SelfDelete { get; set; }

    public string DefaultInstallLocation { get; set; } = string.Empty;

    public bool ShowUnsupportedApps { get; set; } = true;

    public bool ShowOtherPlatforms { get; set; }
}
