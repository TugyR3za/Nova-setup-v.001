using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using NovaSetup.Models;

namespace NovaSetup.Views.Converters;

public sealed class InstallQueueStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var resourceKey = value is InstallQueueStatus status
            ? status switch
            {
                InstallQueueStatus.Downloading => "AccentBrush",
                InstallQueueStatus.Installing => "AccentBrush",
                InstallQueueStatus.Done => "SuccessBrush",
                InstallQueueStatus.Failed => "DangerBrush",
                InstallQueueStatus.Skipped => "WarningBrush",
                InstallQueueStatus.Cancelled => "WarningBrush",
                _ => "TextMutedBrush"
            }
            : "TextMutedBrush";

        if (TryResolveBrush(resourceKey, out var brush))
        {
            return brush;
        }

        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

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
