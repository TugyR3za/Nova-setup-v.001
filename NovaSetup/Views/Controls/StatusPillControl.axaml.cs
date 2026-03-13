using Avalonia;
using Avalonia.Controls;
using NovaSetup.Views.Converters;

namespace NovaSetup.Views.Controls;

public partial class StatusPillControl : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<StatusPillControl, string?>(nameof(Text));

    public static readonly StyledProperty<StatusTone> ToneProperty =
        AvaloniaProperty.Register<StatusPillControl, StatusTone>(nameof(Tone));

    private Border? _pillBorder;
    private TextBlock? _label;

    public StatusPillControl()
    {
        InitializeComponent();
        _pillBorder = this.FindControl<Border>("PART_Pill");
        _label = this.FindControl<TextBlock>("PART_Label");
        UpdateToneState();
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public StatusTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    private void UpdateToneState()
    {
        if (_pillBorder is null || _label is null)
        {
            return;
        }

        _pillBorder.Classes.Set("success", Tone == StatusTone.Success);
        _pillBorder.Classes.Set("info", Tone == StatusTone.Info);
        _pillBorder.Classes.Set("warning", Tone == StatusTone.Warning);
        _pillBorder.Classes.Set("error", Tone == StatusTone.Error);
        _label.Classes.Set("success", Tone == StatusTone.Success);
        _label.Classes.Set("info", Tone == StatusTone.Info);
        _label.Classes.Set("warning", Tone == StatusTone.Warning);
        _label.Classes.Set("error", Tone == StatusTone.Error);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ToneProperty)
        {
            UpdateToneState();
        }
    }
}
