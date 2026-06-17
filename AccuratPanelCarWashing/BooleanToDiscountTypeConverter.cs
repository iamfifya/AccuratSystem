using System;
using System.Globalization;
using System.Windows.Data;

namespace AccuratPanelCarWashing
{
    public class BooleanToDiscountTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPercentage)
            {
                return isPercentage ? "Тип: Процент (%)" : "Тип: Фиксированная сумма (₽)";
            }
            return "Не определено";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
