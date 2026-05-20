namespace AccuratSystem.Contracts.Models
{
    public class User
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public int Role { get; set; }
        public bool IsActive { get; set; } = true;
        public int? BranchId { get; set; }
        public decimal BaseWagePercentage { get; set; }

        // Навигационное свойство (без ? для C# 7.3)
        public Branch Branch { get; set; }
    }
}