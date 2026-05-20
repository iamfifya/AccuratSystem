using System;
using System.Collections.Generic;

namespace AccuratPanelCarWashing.Models
{
    public class Appointment
    {
        public int Id { get; set; }
        public string CarNumber { get; set; } = string.Empty;
        public string CarModel { get; set; } = string.Empty;
        public string CarBodyType { get; set; } = string.Empty;
        public DateTime AppointmentDate { get; set; }
        public int DurationMinutes { get; set; } = 60;
        public DateTime EndTime => AppointmentDate.AddMinutes(DurationMinutes);
        public List<int> ServiceIds { get; set; } = new List<int>();
        public decimal ExtraCost { get; set; }
        public string ExtraCostReason { get; set; } = string.Empty;
        public int BodyTypeCategory { get; set; } = 1;
        public int BoxNumber { get; set; }
        public string Notes { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public int? OrderId { get; set; }
        public int BranchId { get; set; }
        public string Department { get; set; } = "Wash";
    }
}