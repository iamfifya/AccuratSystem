using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AccuratPanelCarWashing
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value as string;
            if (status == "✓ Выполнена")
                return new SolidColorBrush(Color.FromRgb(39, 174, 96)); // Зеленый
            if (status == "⚠️ Просрочена")
                return new SolidColorBrush(Color.FromRgb(231, 76, 60)); // Красный
            return new SolidColorBrush(Color.FromRgb(241, 196, 15)); // Желтый
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
