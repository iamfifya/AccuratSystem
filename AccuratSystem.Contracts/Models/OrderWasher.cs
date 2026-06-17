using System;

namespace AccuratSystem.Contracts.Models
{
    public class OrderWasher
    {
        public int OrderId { get; set; }
        public int UserId { get; set; }
        public decimal SplitShare { get; set; }

        // Снапшот заработка (замораживаем сумму здесь)
        public decimal EarnedAmount { get; set; }

        public Order Order { get; set; }
        public User Washer { get; set; }
    }
}
