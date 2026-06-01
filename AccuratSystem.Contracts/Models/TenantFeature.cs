using System;
using System.Collections.Generic;
using System.Text;

namespace AccuratSystem.Contracts.Models
{
    public class TenantFeature
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }

        public bool IsUpsellEnabled { get; set; } = false;
        public bool IsStorageEnabled { get; set; } = false;
        public bool IsCrmMarketingEnabled { get; set; } = false;
        public bool IsTelegramBossEnabled { get; set; } = false;

        // ДОБАВИЛИ НАВИГАЦИОННОЕ СВОЙСТВО, чтобы работал Include() в контроллере
        public Company Company { get; set; }
    }
}
