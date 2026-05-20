namespace AccuratSystem.Contracts.Models
{
    /// <summary>
    /// Запись в расписании сотрудника.
    /// Только данные, без навигационных свойств (для совместимости с C# 7.3).
    /// </summary>
    public class EmployeeScheduleEntry
    {
        public int EmployeeId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public int Day { get; set; }
        public string Status { get; set; } = string.Empty;

        // Навигационное свойство убрано.
        // Если нужно получить данные сотрудника — делайте отдельный запрос по EmployeeId.
    }
}