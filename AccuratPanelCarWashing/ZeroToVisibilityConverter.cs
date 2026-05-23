using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AccuratPanelCarWashing
{
    public class ZeroToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Visibility.Collapsed;

            // Если это число (decimal или int)
            if (value is decimal decValue)
                return decValue > 0 ? Visibility.Visible : Visibility.Collapsed;

            if (value is int intValue)
                return intValue > 0 ? Visibility.Visible : Visibility.Collapsed;

            // Если это строка — проверяем, не пустая ли она
            if (value is string strValue)
                return !string.IsNullOrWhiteSpace(strValue) ? Visibility.Visible : Visibility.Collapsed;

            return Visibility.Visible; // По умолчанию показываем, если тип неопределен
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}