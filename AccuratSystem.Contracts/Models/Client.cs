using System;
using Newtonsoft.Json; // <-- Добавь этот using для [JsonIgnore]

namespace AccuratSystem.Contracts.Models
{
    public class Client
    {
        public int Id { get; set; }

        // 💥 ИЗОЛЯЦИЯ ТЕНАНТА
        public int CompanyId { get; set; }
        public Company Company { get; set; }

        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string CarModel { get; set; } = string.Empty;
        public string CarNumber { get; set; } = string.Empty;
        public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
        public DateTime? LastVisitDate { get; set; }
        public decimal TotalSpent { get; set; }
        public int VisitsCount { get; set; }
        public decimal DefaultDiscountPercent { get; set; }
        public string Notes { get; set; } = string.Empty;

        // Это свойство будет использоваться в CustomComboBox для отображения в списке
        public string DisplayInfo
        {
            get
            {
                // Формируем строку: "Иванов И.И. | 8999..."
                // Если телефона нет, выводим только ФИО
                return string.IsNullOrEmpty(Phone)
                    ? FullName
                    : $"{FullName} | {Phone}";
            }
        }

        // === БЫСТРЫЙ ФИКС: Свойство-помощник для UI ===
        // [JsonIgnore] говорит сериализатору игнорировать это свойство при обмене с сервером
        [JsonIgnore]
        public decimal AverageCheck => VisitsCount > 0 ? TotalSpent / VisitsCount : 0;
    }
}