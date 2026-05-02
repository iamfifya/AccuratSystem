using System.Collections.Generic;

namespace Accurat.WebAPI.Models
{
    public class Branch
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int Type { get; set; } // 1 - Автомойка, 2 - Сервис

        // Связи
        public List<User> Users { get; set; } = new();
        public List<Order> Orders { get; set; } = new();
        public List<Shift> Shifts { get; set; } = new();
    }
}