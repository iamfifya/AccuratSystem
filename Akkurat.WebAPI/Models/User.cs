namespace Accurat.WebAPI.Models
{
    public class User
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public int Role { get; set; } // 1: Директор, 2: Админ, 3: Мойщик, 4: Механик
        public bool IsActive { get; set; } = true;

        // Привязка к филиалу (У директора будет null)
        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }
    }
}