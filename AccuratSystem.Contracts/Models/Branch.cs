using System.Collections.Generic;

namespace AccuratSystem.Contracts.Models
{
    public class Branch
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int Type { get; set; }

        public int WashBaysCount { get; set; }
        public int ServiceLiftsCount { get; set; }

        public int CompanyId { get; set; }
        public Company Company { get; set; }

        // Навигационные свойства убраны для C# 7.3 совместимости в Contracts
        // Связи настраиваются в AppDbContext бэкенда
    }
}