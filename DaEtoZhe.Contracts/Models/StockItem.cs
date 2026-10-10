using DaEtoZhe.Contracts.Enums;

namespace DaEtoZhe.Contracts.Models
{
    /// <summary>Позиция складской номенклатуры (химия, расходники, запчасти).</summary>
    public class StockItem
    {
        public int Id { get; set; }
        /// <summary>Владелец-тенант.</summary>
        public int CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        /// <summary>Артикул/SKU. Пустая строка = без артикула. Уникален в рамках компании.</summary>
        public string Article { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        /// <summary>Базовая единица: в ней хранятся остатки и происходят списания.</summary>
        public StockUnit Unit { get; set; }
        /// <summary>Единица приёмки (канистра, коробка). Двойная единица — задел на сканер и документы.</summary>
        public StockUnit PurchaseUnit { get; set; }
        /// <summary>Сколько базовых единиц в одной единице приёмки (канистра 5 л → 5).</summary>
        public decimal PurchaseRatio { get; set; } = 1m;
        /// <summary>Минимальный остаток для подсветки «заканчивается».</summary>
        public decimal MinStock { get; set; }
        /// <summary>Последняя цена закупки за базовую единицу.</summary>
        public decimal LastPurchaseCost { get; set; }
        /// <summary>false = в архиве (удаление только архивацией: движения будут ссылаться).</summary>
        public bool IsActive { get; set; } = true;
        public string Notes { get; set; } = string.Empty;

        public StockCategory Category { get; set; }
    }
}