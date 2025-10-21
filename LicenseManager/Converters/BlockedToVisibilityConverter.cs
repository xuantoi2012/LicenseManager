using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LicenseManager.Converters
{
    public class BlockedToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isBlocked = value is bool b && b;
            return isBlocked ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}