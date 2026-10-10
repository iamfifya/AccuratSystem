using DaEtoZhe.Contracts.Enums;
using System;
using System.Collections.Generic;

namespace DaEtoZhe.Contracts.Models
{
    /// <summary>Складской документ (шапка). Позиции = StockMovement с DocumentId.</summary>
    public class StockDocument
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public int BranchId { get; set; }
        public StockDocumentType Type { get; set; }
        /// <summary>Номер вида ПР-00001 / СП-00001. Присваивается сервером.</summary>
        public string Number { get; set; } = string.Empty;
        /// <summary>Поставщик свободным текстом (MVP; словарь поставщиков позже).</summary>
        public string SupplierName { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        /// <summary>Серверное время создания, UTC (конвенция v2).</summary>
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;

        public List<StockMovement> Movements { get; set; } = new List<StockMovement>();
        public Branch Branch { get; set; }
    }
}