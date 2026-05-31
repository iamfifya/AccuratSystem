using System.Collections.Generic;
using AccuratSystem.Contracts.Enums;

namespace AccuratSystem.Contracts.Models
{
    public class Service
    {
        public int Id { get; set; }

        // 💥 ИЗОЛЯЦИЯ ТЕНАНТА
        public int CompanyId { get; set; }
        public Company Company { get; set; }

        public string Name { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        public Dictionary<int, decimal> PriceByBodyType { get; set; } = new Dictionary<int, decimal>();
        public decimal? CustomWagePercentage { get; set; }

        // НОВЫЕ ПОЛЯ ДЛЯ РАЗДЕЛЕНИЯ УСЛУГ
        public bool HasFloatingPrice { get; set; } = false;
        public decimal? BasePriceHint { get; set; }
        public ServiceCategory ServiceCategory { get; set; } = ServiceCategory.Wash;
    }
}