using System.Collections.Generic;

namespace AccuratPanelCarWashing.Models
{
    public class EmployeeShiftReport
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public int CarsWashed { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<int> OrderIds { get; set; } = new List<int>();
    }
}