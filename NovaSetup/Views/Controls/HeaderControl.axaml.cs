using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace NovaSetup.Views.Controls;

public partial class HeaderControl : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<HeaderControl, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<HeaderControl, string?>(nameof(Subtitle));

    public static readonly StyledProperty<string?> AppSelectionStatsProperty =
        AvaloniaProperty.Register<HeaderControl, string?>(nameof(AppSelectionStats));

    public static readonly StyledProperty<ICommand?> HelpCommandProperty =
        AvaloniaProperty.Register<HeaderControl, ICommand?>(nameof(HelpCommand));

    public static readonly StyledProperty<ICommand?> RefreshCatalogCommandProperty =
        AvaloniaProperty.Register<HeaderControl, ICommand?>(nameof(RefreshCatalogCommand));

    public static readonly StyledProperty<int> UpdateAvailableCountProperty =
        AvaloniaProperty.Register<HeaderControl, int>(nameof(UpdateAvailableCount));

    public static readonly StyledProperty<bool> HasUpdatesAvailableProperty =
        AvaloniaProperty.Register<HeaderControl, bool>(nameof(HasUpdatesAvailable));

    public static readonly StyledProperty<ICommand?> OpenUpdatesCommandProperty =
        AvaloniaProperty.Register<HeaderControl, ICommand?>(nameof(OpenUpdatesCommand));

    public HeaderControl()
    {
        InitializeComponent();
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string? AppSelectionStats
    {
        get => GetValue(AppSelectionStatsProperty);
        set => SetValue(AppSelectionStatsProperty, value);
    }

    public ICommand? HelpCommand
    {
        get => GetValue(HelpCommandProperty);
        set => SetValue(HelpCommandProperty, value);
    }

    public ICommand? RefreshCatalogCommand
    {
        get => GetValue(RefreshCatalogCommandProperty);
        set => SetValue(RefreshCatalogCommandProperty, value);
    }

    public int UpdateAvailableCount
    {
        get => GetValue(UpdateAvailableCountProperty);
        set => SetValue(UpdateAvailableCountProperty, value);
    }

    public bool HasUpdatesAvailable
    {
        get => GetValue(HasUpdatesAvailableProperty);
        set => SetValue(HasUpdatesAvailableProperty, value);
    }

    public ICommand? OpenUpdatesCommand
    {
        get => GetValue(OpenUpdatesCommandProperty);
        set => SetValue(OpenUpdatesCommandProperty, value);
    }
}
