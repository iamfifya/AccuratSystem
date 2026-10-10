namespace DaEtoZhe.Contracts.Models
{
    /// <summary>Категория складской номенклатуры. Словарь привязан к компании (тенанту).</summary>
    public class StockCategory
    {
        public int Id { get; set; }
        /// <summary>Владелец-тенант. Категории изолированы между компаниями.</summary>
        public int CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }
}