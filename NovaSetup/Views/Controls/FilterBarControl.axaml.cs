using System;
using System.Collections.Generic;
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

    public static readonly StyledProperty<IReadOnlyList<string>> AvailableCategoriesProperty =
        AvaloniaProperty.Register<FilterBarControl, IReadOnlyList<string>>(
            nameof(AvailableCategories),
            Array.Empty<string>());

    public static readonly StyledProperty<string> SelectedCategoryProperty =
        AvaloniaProperty.Register<FilterBarControl, string>(
            nameof(SelectedCategory),
            "All",
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

    public static readonly StyledProperty<bool> IsArm64FilterProperty =
        AvaloniaProperty.Register<FilterBarControl, bool>(
            nameof(IsArm64Filter),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsUpdatesFilterProperty =
        AvaloniaProperty.Register<FilterBarControl, bool>(
            nameof(IsUpdatesFilter),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsArm64MachineProperty =
        AvaloniaProperty.Register<FilterBarControl, bool>(
            nameof(IsArm64Machine));

    public static readonly StyledProperty<bool> IsGridViewActiveProperty =
        AvaloniaProperty.Register<FilterBarControl, bool>(
            nameof(IsGridViewActive),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<System.Windows.Input.ICommand?> ToggleViewModeCommandProperty =
        AvaloniaProperty.Register<FilterBarControl, System.Windows.Input.ICommand?>(nameof(ToggleViewModeCommand));

    public static readonly StyledProperty<System.Windows.Input.ICommand?> RefreshCatalogCommandProperty =
        AvaloniaProperty.Register<FilterBarControl, System.Windows.Input.ICommand?>(nameof(RefreshCatalogCommand));

    public FilterBarControl()
    {
        InitializeComponent();
    }

    public string? SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public IReadOnlyList<string> AvailableCategories
    {
        get => GetValue(AvailableCategoriesProperty);
        set => SetValue(AvailableCategoriesProperty, value);
    }

    public string SelectedCategory
    {
        get => GetValue(SelectedCategoryProperty);
        set => SetValue(SelectedCategoryProperty, value);
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

    public bool IsArm64Filter
    {
        get => GetValue(IsArm64FilterProperty);
        set => SetValue(IsArm64FilterProperty, value);
    }

    public bool IsUpdatesFilter
    {
        get => GetValue(IsUpdatesFilterProperty);
        set => SetValue(IsUpdatesFilterProperty, value);
    }

    public bool IsArm64Machine
    {
        get => GetValue(IsArm64MachineProperty);
        set => SetValue(IsArm64MachineProperty, value);
    }

    public bool IsGridViewActive
    {
        get => GetValue(IsGridViewActiveProperty);
        set => SetValue(IsGridViewActiveProperty, value);
    }

    public System.Windows.Input.ICommand? ToggleViewModeCommand
    {
        get => GetValue(ToggleViewModeCommandProperty);
        set => SetValue(ToggleViewModeCommandProperty, value);
    }

    public System.Windows.Input.ICommand? RefreshCatalogCommand
    {
        get => GetValue(RefreshCatalogCommandProperty);
        set => SetValue(RefreshCatalogCommandProperty, value);
    }
}
