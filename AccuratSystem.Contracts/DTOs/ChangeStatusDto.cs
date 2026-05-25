namespace AccuratSystem.Contracts.DTOs
{
    /// <summary>
    /// Данные, которые клиент (WPF или MAUI) присылает на сервер для смены статуса.
    /// </summary>
    public class ChangeStatusDto
    {
        public string NewStatus { get; set; } = string.Empty; // Новый статус
        public int? UserId { get; set; }                     // ID сотрудника, который меняет статус
        public string UserName { get; set; } = string.Empty;  // Имя сотрудника для ленты событий
    }
}
