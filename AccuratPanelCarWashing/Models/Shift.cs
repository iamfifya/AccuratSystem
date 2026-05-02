using System;
using System.Collections.Generic;
using System.Linq;

namespace AccuratPanelCarWashing.Models
{
    public class Shift
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public DateTime Date { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Notes { get; set; }
        public bool IsClosed { get; set; } = false;
        public List<int> EmployeeIds { get; set; } = new List<int>();
        public List<CarWashOrder> Orders { get; set; } = new List<CarWashOrder>();

        public int TotalCars => Orders.Count;
        public decimal TotalRevenue => Orders.Sum(o => o.TotalPrice);
    }
}
