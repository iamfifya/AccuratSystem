namespace AccuratSystem.Contracts.Models
{
    public class User
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public int Role { get; set; } // 1 - Директор, 2 - Админ, 3 - Мойщик, 4 - Сотрудник сервиса
        public bool IsActive { get; set; } = true;
        public int? BranchId { get; set; }
        public decimal BaseWagePercentage { get; set; } // Процент
        public decimal BaseSalaryPerShift { get; set; } // Фиксированный оклад за выход (смену)

        // Навигационное свойство (без ? для C# 7.3)
        public Branch Branch { get; set; }

        public string UserDisplayInfo
        {
            get
            {
                string roleName;
                switch (Role)
                {
                    case 1:
                        roleName = "Директор";
                        break;
                    case 2:
                        roleName = "Админ";
                        break;
                    case 3:
                        roleName = "Сервис";
                        break;
                    case 4:
                        roleName = "Мойщик";
                        break;
                    default:    
                        roleName = "Сотрудник";
                        break;
                }
                return $"{FullName} ({roleName})";
            }
        }

    }
}