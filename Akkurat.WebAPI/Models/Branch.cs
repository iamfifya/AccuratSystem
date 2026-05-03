using System.Collections.Generic;

namespace Accurat.WebAPI.Models
{
    public class Branch
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int Type { get; set; } // 1 - Автомойка, 2 - Сервис, 3 - Смешанный

        // Новые поля для динамического интерфейса
        public int WashBaysCount { get; set; }    // Количество моечных боксов
        public int ServiceLiftsCount { get; set; } // Количество подъемников сервиса

        // Связи остаются прежними
        public List<User> Users { get; set; } = new();
        public List<Order> Orders { get; set; } = new();
        public List<Shift> Shifts { get; set; } = new();
    }
}