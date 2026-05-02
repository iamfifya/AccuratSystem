using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AccuratPanelCarWashing
{
    public class AppointmentToStyleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isAppointment && isAppointment)
            {
                return Application.Current.FindResource("AppointmentCardStyle");
            }
            return Application.Current.FindResource("OrderCardStyle");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
