using System;
using System.Globalization;
using System.Windows.Data;

namespace AccuratPanelCarWashing
{
    public class DepartmentToZoneLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string dept = value?.ToString();
            // Обычный if/else для совместимости с C# 7.3
            if (dept == "Service")
                return "🔧 Выберите подъемник:";
            else
                return "🚘 Выберите бокс:";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}