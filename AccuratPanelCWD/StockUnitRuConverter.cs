using AccuratPanelCWD.Services;
using AccuratSystem.Contracts.Enums;
using System;
using System.Globalization;
using System.Windows.Data;

namespace AccuratPanelCWD
{
    /// <summary>Единица измерения по-русски для колонок DataGrid.</summary>
    public class StockUnitRuConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is StockUnit unit ? StockUnitNames.Get(unit) : value?.ToString() ?? "";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}