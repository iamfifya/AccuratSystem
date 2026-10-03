using AccuratSystem.Contracts.Enums;

namespace AccuratPanelCWD.Services
{
    /// <summary>Русские названия единиц измерения для UI.</summary>
    public static class StockUnitNames
    {
        public static string Get(StockUnit unit)
        {
            switch (unit)
            {
                case StockUnit.Piece: return "шт";
                case StockUnit.Liter: return "л";
                case StockUnit.Milliliter: return "мл";
                case StockUnit.Kilogram: return "кг";
                case StockUnit.Gram: return "г";
                case StockUnit.Meter: return "м";
                default: return unit.ToString();
            }
        }
    }
}