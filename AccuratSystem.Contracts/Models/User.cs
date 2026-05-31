namespace AccuratSystem.Contracts.Models
{
    public class User
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        // ИЗМЕНЕНИЯ ЗДЕСЬ:
        public int RoleId { get; set; }
        public Role Role { get; set; } // Связь с таблицей Roles

        // ДОБАВЛЯЕМ ЖЕСТКУЮ ПРИВЯЗКУ К КОМПАНИИ:
        public int? CompanyId { get; set; }
        public Company Company { get; set; }

        public bool IsActive { get; set; }
        public int? BranchId { get; set; }
        public decimal BaseWagePercentage { get; set; }
        public decimal BaseSalaryPerShift { get; set; }

        public Branch Branch { get; set; }

        public string UserDisplayInfo
        {
            get
            {
                // Просто берем имя из связанной таблицы. 
                // Если Role еще не подгрузился из БД, выводим дефолт.
                string roleName = Role != null ? Role.Name : "Сотрудник";
                return $"{FullName} ({roleName})";
            }
        }
    }
}