using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AccuratPanelCarWashing
{
    public class HexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                try
                {
                    return (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
                }
                catch
                {
                    return new SolidColorBrush(Color.FromRgb(127, 140, 141)); // Серый по умолчанию
                }
            }
            return new SolidColorBrush(Color.FromRgb(127, 140, 141));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}