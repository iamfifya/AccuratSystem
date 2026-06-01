using System;

namespace AccuratSystem.Contracts.Models
{
    public class OutboxMessage
    {
        public int Id { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAtUtc { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}