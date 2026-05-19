using System;
using System.Collections.Generic;

namespace AccuratSystem.Contracts.Models
{
    /// <summary>
    /// Основная сущность заказа. Обновлена для поддержки детализации услуг и затрат.
    /// </summary>
    public class Order
    {
        public int Id { get; set; }
        public string CarNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public int? BodyTypeId { get; set; }
        public int? BoxId { get; set; }
        public int? LiftId { get; set; }

        // Старое поле оставляем для обратной совместимости, но помечаем как устаревшее
        [Obsolete("Используйте ServiceItems для работы с услугами")]
        public List<int> ServiceIds { get; set; } = new List<int>();

        /// <summary>
        /// Детализированный список услуг с индивидуальными ценами.
        /// </summary>
        public virtual ICollection<OrderServiceItem> ServiceItems { get; set; } = new List<OrderServiceItem>();

        /// <summary>
        /// Список внутренних затрат по заказу.
        /// </summary>
        public virtual ICollection<OrderExpense> Expenses { get; set; } = new List<OrderExpense>();

        /// <summary>
        /// Лента комментариев и изменений статуса.
        /// </summary>
        public virtual ICollection<OrderTimelineEntry> Timeline { get; set; } = new List<OrderTimelineEntry>();

        /// <summary>
        /// Общее поле для статических заметок (не дублируется в ленту событий).
        /// </summary>
        public string? GeneralNotes { get; set; }

        public Enums.OrderStatus Status { get; set; } = Enums.OrderStatus.InProgress;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? FinishedAt { get; set; }
    }
}