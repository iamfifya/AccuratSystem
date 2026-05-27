using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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

        // === НОВОЕ: Время начала текущего статуса ===
        public DateTime? StatusStartTime { get; set; }

        // Свойство для отображения таймера (например: "00:15:30")
        public string TimeInStatus
        {
            get
            {
                if (IsCompleted || StatusStartTime == null) return "";

                // ИСПРАВЛЕНИЕ: Переводим время сервера (UTC) в локальное время компьютера
                var elapsed = DateTime.Now - StatusStartTime.Value.ToLocalTime();
                return elapsed.ToString(@"hh\:mm\:ss");
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public string StatusDisplay
        {
            get
            {
                if (IsAppointment)
                {
                    if (Status == "Предварительная запись" && Time < DateTime.Now)
                        return "⚠️ Просрочена";

                    switch (Status)
                    {
                        case "Предварительная запись": return "📅 Ожидает";
                        case "В работе": return "🔄 В работе";
                        case "Выполнен": return "✅ Выполнен";
                        case "Отменен": return "❌ Отменена";
                        case "Завершен": return "✅ Завершена";
                        default: return Status;
                    }
                }
                return Status;
            }
        }

        public decimal DiscountPercent { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal OriginalTotalPrice { get; set; }

        public string DiscountDisplay => DiscountPercent > 0 ? $"−{DiscountPercent:F0}%" : (DiscountAmount > 0 ? $"−{DiscountAmount:N0} ₽" : " ");
        public bool HasDiscount => DiscountPercent > 0 || DiscountAmount > 0;
        public string OriginalPriceDisplay => OriginalTotalPrice > 0 ? $"{OriginalTotalPrice:N0} ₽" : " ";
        public bool ShowOriginalPrice => HasDiscount && OriginalTotalPrice > 0;

        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


    }
}
