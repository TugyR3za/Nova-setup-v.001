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

    public static readonly StyledProperty<string?> ProfileNameProperty =
        AvaloniaProperty.Register<HeaderControl, string?>(nameof(ProfileName));

    public static readonly StyledProperty<int> SelectedCountProperty =
        AvaloniaProperty.Register<HeaderControl, int>(nameof(SelectedCount));

    public static readonly StyledProperty<int> RecommendedCountProperty =
        AvaloniaProperty.Register<HeaderControl, int>(nameof(RecommendedCount));

    public static readonly StyledProperty<bool> HasRecommendedAppsProperty =
        AvaloniaProperty.Register<HeaderControl, bool>(nameof(HasRecommendedApps));

    public static readonly StyledProperty<int> UpdateAvailableCountProperty =
        AvaloniaProperty.Register<HeaderControl, int>(nameof(UpdateAvailableCount));

    public static readonly StyledProperty<bool> HasUpdatesAvailableProperty =
        AvaloniaProperty.Register<HeaderControl, bool>(nameof(HasUpdatesAvailable));

    public static readonly StyledProperty<string?> CurrentPlatformProperty =
        AvaloniaProperty.Register<HeaderControl, string?>(nameof(CurrentPlatform));

    public static readonly StyledProperty<ICommand?> HelpCommandProperty =
        AvaloniaProperty.Register<HeaderControl, ICommand?>(nameof(HelpCommand));

    public static readonly StyledProperty<ICommand?> RefreshCatalogCommandProperty =
        AvaloniaProperty.Register<HeaderControl, ICommand?>(nameof(RefreshCatalogCommand));

    public static readonly StyledProperty<ICommand?> ShowUpdatesFilterCommandProperty =
        AvaloniaProperty.Register<HeaderControl, ICommand?>(nameof(ShowUpdatesFilterCommand));

    public static readonly StyledProperty<ICommand?> ShowRecommendedFilterCommandProperty =
        AvaloniaProperty.Register<HeaderControl, ICommand?>(nameof(ShowRecommendedFilterCommand));

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

    public string? ProfileName
    {
        get => GetValue(ProfileNameProperty);
        set => SetValue(ProfileNameProperty, value);
    }

    public int SelectedCount
    {
        get => GetValue(SelectedCountProperty);
        set => SetValue(SelectedCountProperty, value);
    }

    public int RecommendedCount
    {
        get => GetValue(RecommendedCountProperty);
        set => SetValue(RecommendedCountProperty, value);
    }

    public bool HasRecommendedApps
    {
        get => GetValue(HasRecommendedAppsProperty);
        set => SetValue(HasRecommendedAppsProperty, value);
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

    public string? CurrentPlatform
    {
        get => GetValue(CurrentPlatformProperty);
        set => SetValue(CurrentPlatformProperty, value);
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

    public ICommand? ShowUpdatesFilterCommand
    {
        get => GetValue(ShowUpdatesFilterCommandProperty);
        set => SetValue(ShowUpdatesFilterCommandProperty, value);
    }

    public ICommand? ShowRecommendedFilterCommand
    {
        get => GetValue(ShowRecommendedFilterCommandProperty);
        set => SetValue(ShowRecommendedFilterCommandProperty, value);
    }
}
