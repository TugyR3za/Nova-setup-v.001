using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using NovaSetup.Models;

namespace NovaSetup.Views.Controls;

public partial class AppTileControl : UserControl
{
    public static readonly StyledProperty<AppItem?> ItemProperty =
        AvaloniaProperty.Register<AppTileControl, AppItem?>(nameof(Item));

    public static readonly StyledProperty<ICommand?> OpenPublisherCommandProperty =
        AvaloniaProperty.Register<AppTileControl, ICommand?>(nameof(OpenPublisherCommand));

    public static readonly StyledProperty<ICommand?> ShowAppDetailsCommandProperty =
        AvaloniaProperty.Register<AppTileControl, ICommand?>(nameof(ShowAppDetailsCommand));

    public AppTileControl()
    {
        InitializeComponent();
    }

    public AppItem? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public ICommand? OpenPublisherCommand
    {
        get => GetValue(OpenPublisherCommandProperty);
        set => SetValue(OpenPublisherCommandProperty, value);
    }

    public ICommand? ShowAppDetailsCommand
    {
        get => GetValue(ShowAppDetailsCommandProperty);
        set => SetValue(ShowAppDetailsCommandProperty, value);
    }
}
