using System.Collections.Generic;

namespace AccuratSystem.Contracts.Models
{
    /// <summary>
    /// График работы сотрудника на месяц.
    /// Используется для обмена данными между API и клиентами.
    /// </summary>
    public class EmployeeSchedule
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public Dictionary<int, string> Days { get; set; } = new Dictionary<int, string>();
    }
}