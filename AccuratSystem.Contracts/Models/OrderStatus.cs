namespace AccuratSystem.Contracts.Models
{
    public class OrderStatus
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;

        // Цвет бейджика (например "#27AE60")
        public string ColorHex { get; set; } = "#7F8C8D";

        public int SortOrder { get; set; }

        public string DisplayName => $"{Icon} {Name}";
    }
}