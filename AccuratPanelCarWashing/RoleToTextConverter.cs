using System;
using System.Globalization;
using System.Windows.Data;

namespace AccuratPanelCarWashing
{
    public class RoleToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int role)
            {
                switch (role)
                {
                    case 1: return "Директор";
                    case 2: return "Администратор";
                    case 3: return "Мойщик";
                    case 4: return "Сотрудник сервиса";
                    default: return "Неизвестно";
                }
            }
            return "Ошибка";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}