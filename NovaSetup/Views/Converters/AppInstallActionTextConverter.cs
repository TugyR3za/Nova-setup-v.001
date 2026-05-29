using System;
using System.Globalization;
using Avalonia.Data.Converters;
using NovaSetup.Models;

namespace NovaSetup.Views.Converters;

public sealed class AppInstallActionTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is AppItem { IsInstalled: true, HasUpdateAvailable: true }
            ? "Update"
            : "Install";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
