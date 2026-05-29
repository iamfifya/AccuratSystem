namespace AccuratSystem.Contracts.Models
{
    public class PaymentMethod
    {
        public int Id { get; set; }

        // Привязка к тенанту (компании)
        public int CompanyId { get; set; }
        public Company Company { get; set; }

        public string Name { get; set; } = string.Empty; // "Наличные", "Карта", "Оплата по счету"
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }
}