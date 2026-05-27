using System;
using System.Collections.Generic;

namespace AccuratSystem.Contracts.Models
{
    public class Order
    {
        public int Id { get; set; }
        public string CarNumber { get; set; } = string.Empty;
        public string CarModel { get; set; } = string.Empty;
        public int BodyTypeCategory { get; set; } = 1;
        public string CarBodyType { get; set; } = string.Empty;
        public string Department { get; set; } = "Wash";

        public DateTime Time { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "В работе";
        public string PaymentMethod { get; set; } = "Наличные";
        public string Notes { get; set; } = string.Empty;

        public int BoxNumber { get; set; }
        public bool IsAppointment { get; set; }
        public int DurationMinutes { get; set; } = 60;

        public decimal TotalPrice { get; set; }
        public decimal OriginalTotalPrice { get; set; }
        public decimal ExtraCost { get; set; }
        public string ExtraCostReason { get; set; } = string.Empty;
        public decimal DiscountPercent { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalPrice { get; set; }
        public DateTime? CurrentStatusStartTime { get; set; }


        public List<int> ServiceIds { get; set; } = new List<int>();

        public int ShiftId { get; set; }
        public int? ClientId { get; set; }
        public int BranchId { get; set; }

        // Кто именно сидел за кассой и оформлял этот заказ
        public int? AdminId { get; set; }

        // В конец класса Order в Contracts/Models/Order.cs добавь:
        public Branch Branch { get; set; }

        public List<OrderWasher> OrderWashers { get; set; } = new List<OrderWasher>();

        // НОВЫЕ ПОЛЯ ДЛЯ СЕРВИСА
        public string GeneralNotes { get; set; } = string.Empty;
        public DateTime? FinishedAt { get; set; }
    }
}