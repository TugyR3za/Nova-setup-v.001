using System;
using System.Globalization;
using Avalonia.Data.Converters;
using NovaSetup.Models;

namespace NovaSetup.Views.Converters;

public sealed class InstallQueueStatusToProgressVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is InstallQueueStatus status &&
               (status == InstallQueueStatus.Downloading || status == InstallQueueStatus.Installing);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
