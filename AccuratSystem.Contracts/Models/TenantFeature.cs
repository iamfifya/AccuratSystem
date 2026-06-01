using System;
using System.Collections.Generic;
using System.Text;

namespace AccuratSystem.Contracts.Models
{
    public class TenantFeature
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }

        public bool IsUpsellEnabled { get; set; } = false; // Модуль Умный кассир, который предлагает дополнительные услуги при продаже
        public bool IsServicesEnabled { get; set; } = true; // Модуль Управления услугами, который позволяет создавать и управлять услугами, которые предоставляет компания
        public bool IsCrmMarketingEnabled { get; set; } = false; // Модуль CRM и маркетинга, который позволяет управлять клиентами, создавать маркетинговые кампании и анализировать их эффективность
        public bool IsTelegramBossEnabled { get; set; } = false; // Модуль Telegram Босс, который позволяет получать уведомления о продажах и других событиях в Telegram
        public bool IsReputationEnabled { get; set; } = false; // Модуль Репутация, который позволяет управлять отзывами клиентов и анализировать их для улучшения качества обслуживания

        // ДОБАВИЛИ НАВИГАЦИОННОЕ СВОЙСТВО, чтобы работал Include() в контроллере
        public Company Company { get; set; }
    }
}
