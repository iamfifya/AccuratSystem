namespace DaEtoZhe.Contracts.Models
{
    /// <summary>Текущий остаток позиции на складе филиала.</summary>
    public class StockBalance
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        /// <summary>Остатки хранятся в разрезе филиала (склад филиала, не компании).</summary>
        public int BranchId { get; set; }
        public decimal Quantity { get; set; }
        /// <summary>Скользящая средняя себестоимость за базовую единицу.</summary>
        public decimal AvgCost { get; set; }

        public StockItem Item { get; set; }
        public Branch Branch { get; set; }

        /// <summary>Стоимость остатка по скользящей средней (для UI).</summary>
        public decimal TotalValue => Quantity * AvgCost;

        /// <summary>Отрицательный остаток.</summary>
        public bool IsNegative => Quantity < 0;

        /// <summary>На уровне минимума или ниже (MinStock задан).</summary>
        public bool IsLow => Item != null && Item.MinStock > 0 && Quantity <= Item.MinStock;
    }
}