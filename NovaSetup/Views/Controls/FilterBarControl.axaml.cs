using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace NovaSetup.Views.Controls;

public partial class FilterBarControl : UserControl
{
    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<FilterBarControl, string?>(
            nameof(SearchText),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsAllFilterProperty =
        AvaloniaProperty.Register<FilterBarControl, bool>(
            nameof(IsAllFilter),
            defaultValue: true,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsGamesFilterProperty =
        AvaloniaProperty.Register<FilterBarControl, bool>(
            nameof(IsGamesFilter),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsDriversFilterProperty =
        AvaloniaProperty.Register<FilterBarControl, bool>(
            nameof(IsDriversFilter),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsRecommendedFilterProperty =
        AvaloniaProperty.Register<FilterBarControl, bool>(
            nameof(IsRecommendedFilter),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsDevToolsFilterProperty =
        AvaloniaProperty.Register<FilterBarControl, bool>(
            nameof(IsDevToolsFilter),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsUtilitiesFilterProperty =
        AvaloniaProperty.Register<FilterBarControl, bool>(
            nameof(IsUtilitiesFilter),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsUpdatesFilterProperty =
        AvaloniaProperty.Register<FilterBarControl, bool>(
            nameof(IsUpdatesFilter),
            defaultBindingMode: BindingMode.TwoWay);

    public FilterBarControl()
    {
        InitializeComponent();
    }

    public string? SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public bool IsAllFilter
    {
        get => GetValue(IsAllFilterProperty);
        set => SetValue(IsAllFilterProperty, value);
    }

    public bool IsGamesFilter
    {
        get => GetValue(IsGamesFilterProperty);
        set => SetValue(IsGamesFilterProperty, value);
    }

    public bool IsDriversFilter
    {
        get => GetValue(IsDriversFilterProperty);
        set => SetValue(IsDriversFilterProperty, value);
    }

    public bool IsRecommendedFilter
    {
        get => GetValue(IsRecommendedFilterProperty);
        set => SetValue(IsRecommendedFilterProperty, value);
    }

    public bool IsDevToolsFilter
    {
        get => GetValue(IsDevToolsFilterProperty);
        set => SetValue(IsDevToolsFilterProperty, value);
    }

    public bool IsUtilitiesFilter
    {
        get => GetValue(IsUtilitiesFilterProperty);
        set => SetValue(IsUtilitiesFilterProperty, value);
    }

    public bool IsUpdatesFilter
    {
        get => GetValue(IsUpdatesFilterProperty);
        set => SetValue(IsUpdatesFilterProperty, value);
    }
}
