using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace NovaSetup.Views;

public partial class AboutView : UserControl
{
    public static readonly StyledProperty<string?> VersionTextProperty =
        AvaloniaProperty.Register<AboutView, string?>(nameof(VersionText));

    public static readonly StyledProperty<string?> GitHubUrlProperty =
        AvaloniaProperty.Register<AboutView, string?>(nameof(GitHubUrl));

    public static readonly StyledProperty<ICommand?> OpenGitHubCommandProperty =
        AvaloniaProperty.Register<AboutView, ICommand?>(nameof(OpenGitHubCommand));

    public AboutView()
    {
        InitializeComponent();
    }

    public string? VersionText
    {
        get => GetValue(VersionTextProperty);
        set => SetValue(VersionTextProperty, value);
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
}
