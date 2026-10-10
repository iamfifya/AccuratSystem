using System;
using DaEtoZhe.Contracts.Enums;

namespace DaEtoZhe.Contracts.Models
{
    public class OrderTimelineEntry
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public TimelineEntryType EntryType { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? RelatedEntityId { get; set; }
    }
}