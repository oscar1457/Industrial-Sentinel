using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace IndustrialSentinel.App.Utilities;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;
        if (parameter is string text && text.Equals("Invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Visibility visibility && visibility == Visibility.Visible;
    }
}
