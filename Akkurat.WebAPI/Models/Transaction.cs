using System;

namespace Accurat.WebAPI.Models
{
    public class Transaction
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }

        public int? ShiftId { get; set; }
        public Shift? Shift { get; set; }

        public int? EmployeeId { get; set; }
        public User? Employee { get; set; }

        public decimal Amount { get; set; }
        public string Type { get; set; } = string.Empty; // "Приход", "Расход", "Аванс"
        public string Comment { get; set; } = string.Empty;
        public DateTime DateTime { get; set; } = DateTime.UtcNow;

        public string Department { get; set; } // "Wash", "Service" или "General" (для общих расходов филиала)
    }
}