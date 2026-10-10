using System;

namespace DaEtoZhe.Contracts.Models
{
    /// <summary>Запчасть, списанная со склада на ремонт автомобиля.</summary>
    public class VehicleRepairPart
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }
        public int StockItemId { get; set; }
        /// <summary>Филиал, с которого списали (снапшот на момент списания).</summary>
        public int BranchId { get; set; }
        public decimal Quantity { get; set; }
        /// <summary>Цена за единицу на момент списания (скользящая средняя).</summary>
        public decimal CostPrice { get; set; }
        /// <summary>Ссылка на движение склада (журнал = источник правды).</summary>
        public int? StockMovementId { get; set; }
        public DateTime AddedAt { get; set; }
        public string AddedBy { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;

        public decimal TotalCost => Quantity * CostPrice;

        public Vehicle Vehicle { get; set; }
        public StockItem StockItem { get; set; }
        public StockMovement StockMovement { get; set; }
    }
}