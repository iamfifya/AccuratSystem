using System;
using System.Collections.Generic;

namespace AccuratPanelCarWashing.Models
{
    public class Appointment
    {
        public int Id { get; set; }
        public string CarNumber { get; set; }
        public string CarModel { get; set; }
        public string CarBodyType { get; set; }
        public DateTime AppointmentDate { get; set; } // Дата и время записи
        public int DurationMinutes { get; set; } // Длительность мойки в минутах
        public DateTime EndTime => AppointmentDate.AddMinutes(DurationMinutes);
        public List<int> ServiceIds { get; set; } = new List<int>();
        public decimal ExtraCost { get; set; }
        public string ExtraCostReason { get; set; }
        public int BodyTypeCategory { get; set; } = 1; // 1-4 категория кузова
        public int BoxNumber { get; set; } // Желаемый бокс (1,2,3)
        public string Notes { get; set; }
        public bool IsCompleted { get; set; } // Выполнена ли запись
        public int? OrderId { get; set; } // ID созданного заказа после выполнения
    }
}
