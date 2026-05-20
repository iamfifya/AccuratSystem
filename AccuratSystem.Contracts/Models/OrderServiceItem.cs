using System;

namespace AccuratSystem.Contracts.Models
{
    public class OrderServiceItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ServiceId { get; set; }

        public decimal ActualPrice { get; set; }
        public string PriceNote { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}