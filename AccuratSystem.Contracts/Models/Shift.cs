using System;
using System.Collections.Generic;

namespace AccuratSystem.Contracts.Models
{
    public class Shift
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public DateTime Date { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsClosed { get; set; }
        public string Notes { get; set; } = string.Empty;
        public List<int> EmployeeIds { get; set; } = new List<int>();

        // Навигационное свойство (без ? для C# 7.3)
        public Branch Branch { get; set; }
    }
}