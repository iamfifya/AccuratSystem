using System;
using System.Collections.Generic;

namespace Accurat.WebAPI.Models
{
    public class Order
    {
        public int Id { get; set; }
        public string CarNumber { get; set; } = string.Empty;
        public string CarModel { get; set; } = string.Empty;
        public int BodyTypeCategory { get; set; } = 1;
        public string? CarBodyType { get; set; }
        public string Department { get; set; } = "Wash"; // "Wash" или "Service"

        public DateTime Time { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Выполняется";
        public string PaymentMethod { get; set; } = "Наличные";
        public string? Notes { get; set; }

        public int BoxNumber { get; set; }
        public bool IsAppointment { get; set; }
        public int DurationMinutes { get; set; } = 60;

        public decimal TotalPrice { get; set; }
        public decimal OriginalTotalPrice { get; set; }
        public decimal ExtraCost { get; set; }
        public string? ExtraCostReason { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalPrice { get; set; }

        public List<int> ServiceIds { get; set; } = new();

        public int ShiftId { get; set; }
        public int? WasherId { get; set; }
        public int? ClientId { get; set; }

        //  ДОБАВЛЕНЫ СВОЙСТВА ФИЛИАЛА 
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }
    }
}