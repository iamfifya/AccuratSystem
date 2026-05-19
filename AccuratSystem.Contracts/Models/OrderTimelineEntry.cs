using System;
using AccuratSystem.Contracts.Enums;

namespace AccuratSystem.Contracts.Models
{
    /// <summary>
    /// Лента событий заказа. Хранит историю изменений, комментарии и системные записи.
    /// </summary>
    public class OrderTimelineEntry
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string CreatedBy { get; set; } = string.Empty; // Логин или имя сотрудника
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public TimelineEntryType EntryType { get; set; }
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Опциональная ссылка на связанный объект (например, Id изменённой услуги или расхода).
        /// </summary>
        public int? RelatedEntityId { get; set; }

        public virtual Order Order { get; set; }
    }
}