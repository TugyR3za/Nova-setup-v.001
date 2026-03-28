using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace NovaSetup.Views;

public partial class AboutView : UserControl
{
    public static readonly StyledProperty<string?> VersionTextProperty =
        AvaloniaProperty.Register<AboutView, string?>(nameof(VersionText));

    public static readonly StyledProperty<string?> CurrentVersionTextProperty =
        AvaloniaProperty.Register<AboutView, string?>(nameof(CurrentVersionText));

    public static readonly StyledProperty<string?> RemoteVersionTextProperty =
        AvaloniaProperty.Register<AboutView, string?>(nameof(RemoteVersionText));

    public static readonly StyledProperty<string?> GitHubUrlProperty =
        AvaloniaProperty.Register<AboutView, string?>(nameof(GitHubUrl));

    public static readonly StyledProperty<ICommand?> OpenGitHubCommandProperty =
        AvaloniaProperty.Register<AboutView, ICommand?>(nameof(OpenGitHubCommand));

    public static readonly StyledProperty<ICommand?> CheckForUpdatesCommandProperty =
        AvaloniaProperty.Register<AboutView, ICommand?>(nameof(CheckForUpdatesCommand));

    public static readonly StyledProperty<ICommand?> HelpCommandProperty =
        AvaloniaProperty.Register<AboutView, ICommand?>(nameof(HelpCommand));

    public static readonly StyledProperty<string?> UpdateStatusTextProperty =
        AvaloniaProperty.Register<AboutView, string?>(nameof(UpdateStatusText));

    public static readonly StyledProperty<bool> IsUpdateStatusVisibleProperty =
        AvaloniaProperty.Register<AboutView, bool>(nameof(IsUpdateStatusVisible));

    public AboutView()
    {
        InitializeComponent();
    }

    public string? VersionText
    {
        get => GetValue(VersionTextProperty);
        set => SetValue(VersionTextProperty, value);
    }

    public string? CurrentVersionText
    {
        get => GetValue(CurrentVersionTextProperty);
        set => SetValue(CurrentVersionTextProperty, value);
    }

    public string? RemoteVersionText
    {
        get => GetValue(RemoteVersionTextProperty);
        set => SetValue(RemoteVersionTextProperty, value);
    }

    public string? GitHubUrl
    {
        get => GetValue(GitHubUrlProperty);
        set => SetValue(GitHubUrlProperty, value);
    }

    public ICommand? OpenGitHubCommand
    {
        get => GetValue(OpenGitHubCommandProperty);
        set => SetValue(OpenGitHubCommandProperty, value);
    }

    public ICommand? CheckForUpdatesCommand
    {
        get => GetValue(CheckForUpdatesCommandProperty);
        set => SetValue(CheckForUpdatesCommandProperty, value);
    }

    public ICommand? HelpCommand
    {
        get => GetValue(HelpCommandProperty);
        set => SetValue(HelpCommandProperty, value);
    }

    public string? UpdateStatusText
    {
        get => GetValue(UpdateStatusTextProperty);
        set => SetValue(UpdateStatusTextProperty, value);
    }

    public bool IsUpdateStatusVisible
    {
        get => GetValue(IsUpdateStatusVisibleProperty);
        set => SetValue(IsUpdateStatusVisibleProperty, value);
    }
}
