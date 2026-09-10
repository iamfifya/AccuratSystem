using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AccuratPanelCWD.Models
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

        private string _statusColorHex;

        /// <summary>
        /// Цвет бейджа статуса. Если сервер вернул свой цвет — используем его,
        /// иначе берём фолбэк-палитру по имени статуса (работает без API).
        /// </summary>
        public string StatusColorHex
        {
            get
            {
                // Серверный цвет используем, если он есть и это не дефолтный серый
                if (!string.IsNullOrWhiteSpace(_statusColorHex) && _statusColorHex != "#7F8C8D")
                    return _statusColorHex;

                return GetFallbackStatusColor(Status);
            }
            set
            {
                _statusColorHex = value;
                OnPropertyChanged(nameof(StatusColorHex));
            }
        }

        /// <summary>
        /// Запасная палитра статусов: гарантирует цветные бейджи,
        /// даже если OrderStatuses не загрузились с сервера.
        /// </summary>
        public static string GetFallbackStatusColor(string status)
        {
            switch (status)
            {
                case "В работе":
                    return "#3498DB";   // синий — процесс идёт
                case "Выполнен":
                case "Завершен":
                    return "#27AE60";   // зелёный — успех
                case "Отменен":
                    return "#E74C3C";   // красный — отмена
                case "Предварительная запись":
                case "Запись":
                case "Ожидает":
                    return "#F39C12";   // оранжевый — ожидание
                case "Диагностика":
                case "Ожидание запчастей":
                    return "#9B59B6";   // фиолетовый — сервисные паузы
                default:
                    return "#7F8C8D";   // серый — неизвестный статус
            }
        }

        // === НОВОЕ: Время начала текущего статуса ===
        public DateTime? StatusStartTime { get; set; }

        // Свойство для отображения таймера (например: "00:15:30")
        public string TimeInStatus
        {
            get
            {
                if (IsCompleted || StatusStartTime == null) return "";

                //  Переводим время сервера (UTC) в локальное время компьютера
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
