namespace AccuratSystem.Contracts.Models
{
    public class CarCategory
    {
        public int Id { get; set; }

        // Привязка к компании (тенанту)
        public int CompanyId { get; set; }
        public Company Company { get; set; }

        public string Name { get; set; } = string.Empty;

        // Чтобы в выпадающем списке они шли по порядку, а не вразнобой
        public int SortOrder { get; set; }
    }
}