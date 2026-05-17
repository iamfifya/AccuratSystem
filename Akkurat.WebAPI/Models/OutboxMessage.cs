using System;

namespace Accurat.WebAPI.Models
{
    public class OutboxMessage
    {
        public int Id { get; set; }
        public string EventType { get; set; }
        public string PayloadJson { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? ProcessedAtUtc { get; set; }

        // ИСПРАВЛЕНО: Ставим пустую строку по умолчанию вместо null
        public string ErrorMessage { get; set; } = string.Empty;
    }
}