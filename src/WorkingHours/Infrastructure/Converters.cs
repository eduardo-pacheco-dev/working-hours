using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WorkingHours.Infrastructure;

/// <summary>Visible when the bound boolean is false, collapsed otherwise.</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Collapsed;
}
