using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AccuratPanelCarWashing.Models;
using AccuratSystem.Contracts.Models;

namespace AccuratPanelCarWashing
{
    public class ItemStyleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is OrderDisplayItem item)
            {
                if (item.IsSelected)
                {
                    return item.IsAppointment
                        ? Application.Current.FindResource("SelectedAppointmentCardStyle")
                        : Application.Current.FindResource("SelectedOrderCardStyle");
                }
                else
                {
                    return item.IsAppointment
                        ? Application.Current.FindResource("AppointmentCardStyle")
                        : Application.Current.FindResource("OrderCardStyle");
                }
            }
            return Application.Current.FindResource("OrderCardStyle");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
