using AccuratSystem.Contracts.Enums;
using System;
using System.Collections.Generic;

namespace AccuratSystem.Contracts.DTOs
{
    /// <summary>Аналитика расходников за период по филиалу.</summary>
    public class StockConsumptionReport
    {
        /// <summary>Себестоимость списаний за период (нетто со сторно), ₽.</summary>
        public decimal TotalConsumptionCost { get; set; }
        /// <summary>Стоимость текущих остатков филиала по скользящей средней, ₽.</summary>
        public decimal StockValueTotal { get; set; }
        public List<StockConsumptionItem> TopItems { get; set; } = new List<StockConsumptionItem>();
        public List<StockDailyConsumption> Daily { get; set; } = new List<StockDailyConsumption>();
        public List<StockLowItem> LowStock { get; set; } = new List<StockLowItem>();
    }

    public class StockConsumptionItem
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public StockUnit Unit { get; set; }
        /// <summary>Сколько съедено за период (абсолютное значение).</summary>
        public decimal TotalQuantity { get; set; }
        public decimal TotalCost { get; set; }
    }

    public class StockDailyConsumption
    {
        /// <summary>Бизнес-день филиала (инстант полуночи зоны, как ToWireDate).</summary>
        public DateTime Date { get; set; }
        public string DateLabel { get; set; } = string.Empty;
        public decimal Cost { get; set; }
    }

    public class StockLowItem
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public StockUnit Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal MinStock { get; set; }
    }
}