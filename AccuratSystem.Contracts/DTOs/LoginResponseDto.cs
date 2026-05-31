using AccuratSystem.Contracts.Models;
using System.Collections.Generic;

namespace AccuratSystem.Contracts.DTOs
{
    // Новый запрос от клиента: только логин и пароль
    public class LoginRequestDto
    {
        public string Login { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    // Ответ сервера
    public class LoginResponseDto
    {
        public User User { get; set; }
        public TenantFeaturesDto Features { get; set; }

        // ДОБАВЛЯЕМ СПИСОК ФИЛИАЛОВ
        public List<Branch> AvailableBranches { get; set; } = new List<Branch>();
        public string Message { get; set; } = string.Empty;
    }
}