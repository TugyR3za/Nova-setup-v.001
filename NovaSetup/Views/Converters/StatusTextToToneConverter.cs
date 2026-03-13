using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace NovaSetup.Views.Converters;

public enum StatusTone
{
    Default,
    Info,
    Success,
    Warning,
    Error
}

public sealed class StatusTextToToneConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var statusText = value as string;
        if (string.IsNullOrWhiteSpace(statusText))
        {
            return StatusTone.Default;
        }

        return statusText.Trim().ToLowerInvariant() switch
        {
            "installed" => StatusTone.Success,
            "available" => StatusTone.Info,
            "selected" => StatusTone.Info,
            "recommended" => StatusTone.Info,
            "will be skipped" => StatusTone.Warning,
            "unsupported on this os" => StatusTone.Warning,
            "needs manual install" => StatusTone.Warning,
            "pending restart" => StatusTone.Warning,
            "failed" => StatusTone.Error,
            _ => StatusTone.Default
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
