// SafePriceConverter.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace AccuratPanelCarWashing
{
    public class SafePriceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Dictionary<int, decimal> prices && parameter is string categoryStr)
            {
                if (int.TryParse(categoryStr, out int category))
                {
                    if (prices != null && prices.ContainsKey(category))
                    {
                        return prices[category];
                    }
                }
            }
            return 0m;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
