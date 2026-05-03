using System;
using System.ComponentModel;

namespace AccuratPanelCarWashing.Models
{
    public class OrderDisplayItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public int Id { get; set; }
        public string CarNumber { get; set; }
        public string CarModel { get; set; }
        public DateTime Time { get; set; }
        public int BranchId { get; set; }
        public string Department { get; set; }
        public string WasherName { get; set; }
        public string ServicesList { get; set; }
        public decimal FinalPrice { get; set; }
        public decimal ExtraCost { get; set; }
        public string ExtraCostReason { get; set; }
        public int BoxNumber { get; set; }
        public string Status { get; set; }
        public bool IsAppointment { get; set; }
        public bool IsCompleted { get; set; }
        public int DurationMinutes { get; set; } = 60;
        public string PaymentMethod { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }
        public string StatusDisplay
        {
            get
            {
                if (IsAppointment)
                {
                    // Для записей показываем расширенный статус
                    if (Status == "Предварительная запись" && Time < DateTime.Now)
                        return "⚠️ Просрочена";

                    // ✅ Обычный switch statement для C# 7.3
                    switch (Status)
                    {
                        case "Предварительная запись":
                            return "📅 Ожидает";
                        case "Выполняется":
                            return "🔄 В работе";
                        case "Выполнен":
                            return "✅ Выполнен";
                        case "Отменен":
                            return "❌ Отменена";
                        default:
                            return Status;
                    }
                }
                return Status; // Для обычных заказов
            }
        }

        public decimal DiscountPercent { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal OriginalTotalPrice { get; set; }

        public string DiscountDisplay => DiscountPercent > 0 ? $"−{DiscountPercent:F0}%" : (DiscountAmount > 0 ? $"−{DiscountAmount:N0} ₽" : "");
        public bool HasDiscount => DiscountPercent > 0 || DiscountAmount > 0;
        public string OriginalPriceDisplay => OriginalTotalPrice > 0 ? $"{OriginalTotalPrice:N0} ₽" : "";
        public bool ShowOriginalPrice => HasDiscount && OriginalTotalPrice > 0;
    }
}
