// Models/Schedule.cs
using System.Collections.Generic;

namespace AccuratPanelCarWashing.Models
{
    public class Schedule
    {
        public int Id { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public List<EmployeeSchedule> EmployeeSchedules { get; set; } = new List<EmployeeSchedule>();
    }

    public class EmployeeSchedule
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string Position { get; set; }
        public Dictionary<int, string> Days { get; set; } = new Dictionary<int, string>();
    }
}
