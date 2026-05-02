// Models/Branch.cs
namespace AccuratPanelCarWashing.Models
{
    public class Branch
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int Type { get; set; } // 1 - Автомойка, 2 - Сервис
    }
}
