using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using NovaSetup.Services;

namespace NovaSetup.Views.Converters;

public sealed class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is LogLevel level
            ? level switch
            {
                LogLevel.Error => "DangerBrush",
                LogLevel.Warning => "WarningBrush",
                LogLevel.Success => "SuccessBrush",
                LogLevel.Debug => "TextMutedBrush",
                _ => "TextPrimaryBrush"
            }
            : "TextPrimaryBrush";

        if (TryResolveBrush(key, out var brush))
        {
            return brush;
        }

        if (TryResolveBrush("TextPrimaryBrush", out var fallbackBrush))
        {
            return fallbackBrush;
        }

        return Brushes.Black;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static bool TryResolveBrush(string key, out IBrush brush)
    {
        brush = Brushes.Black;

        if (Application.Current is null)
        {
            return false;
        }

        var themeVariant = Application.Current.ActualThemeVariant;
        if (Application.Current.TryGetResource(key, themeVariant, out var resource) && resource is IBrush themedBrush)
        {
            brush = themedBrush;
            return true;
        }

        if (Application.Current.TryGetResource(key, ThemeVariant.Default, out resource) && resource is IBrush defaultBrush)
        {
            brush = defaultBrush;
            return true;
        }

        return false;
    }
}
