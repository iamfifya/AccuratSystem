using System;
using System.Globalization;
using System.Windows.Data;

namespace AccuratPanelCarWashing
{
    public class BoolToRoleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isAdmin)
            {
                return isAdmin ? "Администратор" : "Мойщик";
            }
            return "Мойщик";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}