using System;
using System.Collections.Generic;
using System.Text;

namespace AccuratSystem.Contracts.Models
{
    public class DiscountRule
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string Name { get; set; } = string.Empty; // "Плохая погода", "Пенсионер"
        public decimal Value { get; set; } // 10.0
        public bool IsPercentage { get; set; } = true; // true = %, false = фиксированная сумма
    }

}
