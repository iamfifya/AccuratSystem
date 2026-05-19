using System;
using System.Collections.Generic;

namespace AccuratSystem.Contracts.Models
{
    /// <summary>
    /// Промежуточная сущность связи "Заказ - Услуга".
    /// Позволяет хранить фактическую цену, комментарий и статус выполнения каждой услуги.
    /// </summary>
    public class OrderServiceItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ServiceId { get; set; }

        /// <summary>
        /// Фактическая цена, зафиксированная для данного заказа.
        /// До подтверждения мастером может равняться 0 или BasePriceHint.
        /// </summary>
        public decimal ActualPrice { get; set; }

        /// <summary>
        /// Комментарий к цене или особенности выполнения.
        /// </summary>
        public string PriceNote { get; set; } = string.Empty;

        /// <summary>
        /// Статус выполнения конкретной услуги: Pending, InProgress, Done.
        /// </summary>
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Навигационные свойства (опционально, для удобства в EF Core)
        public virtual Order Order { get; set; }
        public virtual Service Service { get; set; }
    }
}