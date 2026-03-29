using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace NovaSetup.Views.Converters;

public sealed class VirusTotalRatioToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var ratio = value as string;
        var resourceKey = TryHasDetections(ratio)
            ? "WarningBrush"
            : "SuccessBrush";

        if (TryResolveBrush(resourceKey, out var brush))
        {
            return brush;
        }

        if (TryResolveBrush("TextMutedBrush", out var fallbackBrush))
        {
            return fallbackBrush;
        }

        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static bool TryHasDetections(string? ratio)
    {
        if (string.IsNullOrWhiteSpace(ratio))
        {
            return false;
        }

        var separatorIndex = ratio.IndexOf('/');
        if (separatorIndex <= 0)
        {
            return false;
        }

        return int.TryParse(ratio[..separatorIndex].Trim(), out var detections) && detections > 0;
    }

    private static bool TryResolveBrush(string key, out IBrush brush)
    {
        brush = Brushes.Gray;

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
