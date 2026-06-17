namespace AccuratSystem.Contracts.Models
{
    public class CompanySettings
    {
        // Primary Key и Foreign Key одновременно (Связь 1 к 1)
        public int CompanyId { get; set; }
        public Company Company { get; set; }

        // Финансовые настройки
        public decimal CompanySharePercentage { get; set; } = 65m;

        // Настройки расписания (пригодится для AddEditOrderWindow)
        public int DefaultAppointmentDuration { get; set; } = 60;

        public decimal DayShiftAdminPercentage { get; set; } = 2.5m; // Те самые 2.5% для админов в дневную смену
        public decimal NightShiftWasherPercentage { get; set; } = 50.0m; // 50% для ночи


        // Сюда в будущем можно добавить:
        // public string TelegramBotToken { get; set; }
        // public bool RequireAdminApprovalForDiscounts { get; set; }
    }
}