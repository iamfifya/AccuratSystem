using System;
using AccuratSystem.Contracts.Enums;

namespace AccuratSystem.Contracts.Models
{
    public class OrderExpense
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string Name { get; set; } = string.Empty;
        public ExpenseCategory Category { get; set; }

        public decimal CostPrice { get; set; }
        public decimal ClientPrice { get; set; }
        public int Quantity { get; set; } = 1;
        public string Note { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}