using System;
using System.Globalization;
using System.Windows.Data;

namespace AccuratPanelCarWashing
{
    public class PercentageConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return "0%";

            if (values[0] is decimal washerRevenue && values[1] is decimal totalRevenue)
            {
                if (totalRevenue == 0) return "0%";

                var percentage = (washerRevenue / totalRevenue) * 100m;
                return $"{percentage:F1}%";
            }

            if (values[0] is double washerRevenueDouble && values[1] is double totalRevenueDouble)
            {
                if (totalRevenueDouble == 0) return "0%";

                var percentage = (washerRevenueDouble / totalRevenueDouble) * 100.0;
                return $"{percentage:F1}%";
            }

            return "0%";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}