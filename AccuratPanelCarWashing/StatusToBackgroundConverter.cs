using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AccuratPanelCarWashing
{
    public class StatusToBackgroundConverter : IValueConverter
    {
        // В Converters/StatusToBackgroundConverter.cs:
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                // ✅ Просроченные записи — красный
                if (status.Contains("⚠️") || status.Contains("Просроч"))
                    return new SolidColorBrush(Color.FromRgb(231, 76, 60)); // Красный

                // ✅ Отменённые — серый
                if (status.Contains("❌") || status.Contains("Отмен"))
                    return new SolidColorBrush(Color.FromRgb(149, 165, 166)); // Серый

                // ✅ Выполненные — зелёный
                if (status.Contains("✅") || status.Contains("Выполн"))
                    return new SolidColorBrush(Color.FromRgb(46, 204, 113)); // Зелёный

                // ✅ В работе / конвертированные — синий
                if (status.Contains("🔄") || status.Contains("работ") || status.Contains("Заказ"))
                    return new SolidColorBrush(Color.FromRgb(52, 152, 219)); // Синий

                // ✅ Ожидает — оранжевый
                if (status.Contains("📅") || status.Contains("Ожидает"))
                    return new SolidColorBrush(Color.FromRgb(243, 156, 18)); // Оранжевый
            }

            // По умолчанию
            return new SolidColorBrush(Color.FromRgb(127, 140, 141));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
