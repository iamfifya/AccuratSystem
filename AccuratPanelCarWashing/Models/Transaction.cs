using System;

namespace AccuratPanelCarWashing.Models
{
    public class Transaction
    {
        public int Id { get; set; }

        // ВАЖНО: Добавили привязку к филиалу для PostgreSQL
        public int BranchId { get; set; }

        public int? ShiftId { get; set; }
        public int? EmployeeId { get; set; }

        public decimal Amount { get; set; }
        public string Type { get; set; }
        public string Comment { get; set; }
        public DateTime DateTime { get; set; }

        public string Department { get; set; } // "Wash", "Service" или "General" (для общих расходов филиала)

        // --- Помощники для визуального интерфейса (C# 7.3 compatible) ---
        public string FormattedAmount
        {
            get { return (Type == "Приход" || Type == "Размен") ? $"+{Amount:N0} ₽" : $"-{Amount:N0} ₽"; }
        }

        public string ColorHex
        {
            get { return (Type == "Приход" || Type == "Размен") ? "#27AE60" : "#E74C3C"; }
        }

        public string Icon
        {
            get
            {
                switch (Type)
                {
                    case "Приход": return "💵";
                    case "Расход": return "🛒";
                    case "Аванс мойщику": return "👤";
                    case "Инкассация": return "🏦";
                    case "Размен": return "🪙";
                    default: return "📄";
                }
            }
        }
    }
}
