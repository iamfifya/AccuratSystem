using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;

namespace AccuratPanelCarWashing.Models
{
    public class Service : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public int Id { get; set; }
        public string Name { get; set; }
        public int DurationMinutes { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;

        // Кастомный процент за услугу (если null - берется базовый процент сотрудника)
        public decimal? CustomWagePercentage { get; set; }

        // Новая структура цен по категориям кузова
        public Dictionary<int, decimal> PriceByBodyType { get; set; } = new Dictionary<int, decimal>();

        // Метод для получения цены по категории кузова
        public decimal GetPrice(int bodyTypeCategory)
        {
            if (PriceByBodyType.TryGetValue(bodyTypeCategory, out var price))
                return price;

            // Если нет цены для этой категории, берем цену для категории 1
            if (PriceByBodyType.TryGetValue(1, out var defaultPrice))
                return defaultPrice;

            // Если нет вообще, возвращаем 0
            return 0;
        }
    }
}
