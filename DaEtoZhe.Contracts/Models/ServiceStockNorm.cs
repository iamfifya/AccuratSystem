namespace DaEtoZhe.Contracts.Models
{
    /// <summary>Норма списания: сколько базовых единиц позиции уходит на 1 заказ с этой услугой.</summary>
    public class ServiceStockNorm
    {
        public int Id { get; set; }
        public int ServiceId { get; set; }
        public int ItemId { get; set; }
        /// <summary>Расход базовой единицы на 1 заказ (например 0.08 л шампуня).</summary>
        public decimal Quantity { get; set; }

        public Service Service { get; set; }
        public StockItem Item { get; set; }
    }
}