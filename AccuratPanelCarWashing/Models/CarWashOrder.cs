// CarWashOrder.cs
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace AccuratPanelCarWashing.Models
{
    public class CarWashOrder : IDataErrorInfo
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Введите марку и модель автомобиля")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Марка должна содержать от 2 до 100 символов")]
        public string CarModel { get; set; }

        [Required(ErrorMessage = "Введите государственный номер")]
        [RegularExpression(@"^[АВЕКМНОРСТУХ]\d{3}[АВЕКМНОРСТУХ]{2}\d{2,3}$",
            ErrorMessage = "Неверный формат госномера (пример: А123ВС77)")]
        public string CarNumber { get; set; }
        public bool IsAppointment { get; set; } = false; // Признак предварительной записи
        public int BodyTypeCategory { get; set; } = 1; // 1-4 категория кузова

        public string CarBodyType { get; set; }
        public DateTime Time { get; set; }
        public decimal TotalPrice { get; set; }
        public int? ShiftId { get; set; }
        public int BranchId { get; set; }
        public string Department { get; set; }
        public string Notes { get; set; }
        public List<int> ServiceIds { get; set; } = new List<int>();
        public int BoxNumber { get; set; }
        public int WasherId { get; set; }

        [Range(0, 100000, ErrorMessage = "Дополнительная стоимость должна быть от 0 до 100 000")]
        public decimal ExtraCost { get; set; }
        public int? ClientId { get; set; }
        public string ExtraCostReason { get; set; }

        public decimal DiscountPercent { get; set; } = 0; // Скидка в процентах (0-100)
        public decimal DiscountAmount { get; set; } = 0; // Скидка в деньгах (переопределяет процент)
        public decimal OriginalTotalPrice { get; set; } // Цена услуг ДО скидки

        public decimal BasePrice => TotalPrice;
        public decimal FinalPrice
        {
            get
            {
                decimal baseAmount = TotalPrice + ExtraCost;
                decimal actualDiscount = (DiscountPercent > 0)
                    ? baseAmount * (DiscountPercent / 100m)
                    : DiscountAmount;

                return baseAmount - actualDiscount;
            }
            // Добавляем set, чтобы модель могла принимать данные от API или ViewModel
            set { }
        }
        public string PaymentMethod { get; set; } = "Наличные";
        public int DurationMinutes { get; set; } = 60;

        public string ServicesCount => ServiceIds.Count > 0 ? $"{ServiceIds.Count} услуг" : "нет услуг";
        public string BoxName => $"Бокс {BoxNumber}";

        public string Error => null;
        public string Status { get; set; } = "В работе";

        public string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(CarModel):
                        if (string.IsNullOrWhiteSpace(CarModel))
                            return "Введите марку и модель автомобиля";
                        if (CarModel.Length < 2)
                            return "Марка должна содержать минимум 2 символа";
                        break;
                    case nameof(CarNumber):
                        if (string.IsNullOrWhiteSpace(CarNumber))
                            return "Введите государственный номер";
                        break;
                    case nameof(ExtraCost):
                        if (ExtraCost < 0)
                            return "Дополнительная стоимость не может быть отрицательной";
                        if (ExtraCost > 100000)
                            return "Дополнительная стоимость не может превышать 100 000 ₽";
                        break;
                }
                return null;
            }
        }
    }
}
