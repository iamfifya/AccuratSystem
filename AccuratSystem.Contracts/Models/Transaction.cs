using System;

namespace AccuratSystem.Contracts.Models
{
    public class Transaction
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public int? ShiftId { get; set; }
        public int? EmployeeId { get; set; }

        public decimal Amount { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public DateTime DateTime { get; set; } = DateTime.UtcNow;
        public string Department { get; set; } = string.Empty;

        // Навигационные свойства (без ? для C# 7.3)
        // При использовании: проверяй на null или используй Include в запросе
        public Branch Branch { get; set; }
        public Shift Shift { get; set; }
        public User Employee { get; set; }
    }
}