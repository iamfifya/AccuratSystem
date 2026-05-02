using System;

namespace Accurat.WebAPI.Models
{
    public class Client
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string CarModel { get; set; } = string.Empty;
        public string CarNumber { get; set; } = string.Empty;
        public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
        public DateTime? LastVisitDate { get; set; }
        public decimal TotalSpent { get; set; }
        public int VisitsCount { get; set; }
        public decimal DefaultDiscountPercent { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}