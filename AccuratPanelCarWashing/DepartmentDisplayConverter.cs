using System;
using System.Globalization;
using System.Windows.Data;

namespace AccuratPanelCarWashing // <-- УБРАЛИ .Converters ЗДЕСЬ
{
    public class DepartmentDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "Выберите департамент";

            switch (value.ToString())
            {
                case "Wash": return "💦 Мойка";
                case "Service": return "🔧 Сервис";
                default: return "Выберите департамент";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "Wash";

            switch (value.ToString())
            {
                case "💦 Мойка": return "Wash";
                case "🔧 Сервис": return "Service";
                default: return "Wash";
            }
        }
    }
}