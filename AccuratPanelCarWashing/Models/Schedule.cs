using System.Collections.Generic;

namespace AccuratPanelCarWashing.Models
{
    /// <summary>
    /// График работы сотрудников на месяц.
    /// </summary>
    public class Schedule
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public List<EmployeeSchedule> EmployeeSchedules { get; set; } = new List<EmployeeSchedule>();
    }

    /// <summary>
    /// График одного сотрудника.
    /// </summary>
    public class EmployeeSchedule
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public Dictionary<int, string> Days { get; set; } = new Dictionary<int, string>();
    }
}