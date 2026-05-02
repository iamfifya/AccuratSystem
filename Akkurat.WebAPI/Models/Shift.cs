using System;
using System.Collections.Generic;

namespace Accurat.WebAPI.Models
{
    public class Shift
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }

        public DateTime Date { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsClosed { get; set; }
        public string Notes { get; set; } = string.Empty;

        // Массив ID сотрудников, работающих в смену
        public List<int> EmployeeIds { get; set; } = new();
    }
}