using System;
using System.Globalization;
using Avalonia.Data.Converters;
using NovaSetup.Models;

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

        var normalized = statusText.Trim();

        if (string.Equals(normalized, AppItem.StatusInstalled, StringComparison.OrdinalIgnoreCase))
        {
            return StatusTone.Success;
        }

        if (string.Equals(normalized, AppItem.StatusUpdateAvailable, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, AppItem.StatusAvailable, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, AppItem.StatusSelected, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "recommended", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, AppItem.StatusInstalling, StringComparison.OrdinalIgnoreCase))
        {
            return StatusTone.Info;
        }

        if (string.Equals(normalized, AppItem.StatusWillBeSkipped, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, AppItem.StatusUnsupportedOnCurrentOs, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, AppItem.StatusNeedsManualInstall, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "pending restart", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, AppItem.StatusCancelled, StringComparison.OrdinalIgnoreCase))
        {
            return StatusTone.Warning;
        }

        if (string.Equals(normalized, AppItem.StatusFailed, StringComparison.OrdinalIgnoreCase))
        {
            return StatusTone.Error;
        }

        return StatusTone.Default;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
