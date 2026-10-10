using DaEtoZhe.Contracts.Enums;
using System;

namespace DaEtoZhe.Contracts.Models
{
    /// <summary>Движение остатков: строка документа или автосписание.</summary>
    public class StockMovement
    {
        public int Id { get; set; }
        public int? DocumentId { get; set; }
        public int ItemId { get; set; }
        public int BranchId { get; set; }
        public StockMovementType Type { get; set; }
        /// <summary>Количество со знаком: плюс = приход, минус = списание.</summary>
        public decimal Quantity { get; set; }
        /// <summary>Себестоимость за базовую единицу на момент движения (снапшот).</summary>
        public decimal CostPrice { get; set; }
        /// <summary>Для автосписаний этапа 3: заказ и смена-владелец себестоимости.</summary>
        public int? OrderId { get; set; }
        public int? ShiftId { get; set; }
        /// <summary>Серверное время, UTC (конвенция v2).</summary>
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;

        public StockItem Item { get; set; }
        public StockDocument Document { get; set; }

        /// <summary>Сумма движения по снапшот-цене (для UI).</summary>
        public decimal LineSum => Quantity * CostPrice;
    }
}