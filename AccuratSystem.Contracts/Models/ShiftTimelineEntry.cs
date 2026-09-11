using System;

namespace AccuratSystem.Contracts.Models
{
    /// <summary>
    /// Лента событий смены: открытие, закрытие, X-отчёты, перенос заказов.
    /// </summary>
    public class ShiftTimelineEntry
    {
        public int Id { get; set; }
        public int ShiftId { get; set; }
        public Shift Shift { get; set; }

        public string EventType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int? RelatedEntityId { get; set; }
    }
}