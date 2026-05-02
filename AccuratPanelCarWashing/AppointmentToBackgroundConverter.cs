using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AccuratPanelCarWashing
{
    public class AppointmentToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isAppointment && isAppointment)
            {
                // Возвращаем светло-желтый фон для предварительных записей
                return new SolidColorBrush(Color.FromRgb(255, 248, 225));
            }
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
