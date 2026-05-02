using System.Collections.Generic;
using System.Linq;

namespace MyPanelCarWashing.Models
{
    public class AppData
    {
        public List<User> Users { get; set; } = new List<User>();
        public List<Service> Services { get; set; } = new List<Service>();
        public List<Shift> Shifts { get; set; } = new List<Shift>();
        public List<Appointment> Appointments { get; set; } = new List<Appointment>();
        public List<Client> Clients { get; set; } = new List<Client>();
        public List<Schedule> Schedules { get; set; } = new List<Schedule>();


        private int _nextUserId = 1;
        private int _nextServiceId = 1;
        private int _nextShiftId = 1;
        private int _nextOrderId = 1;
        private int _nextAppointmentId = 1; // Добавляем

        public int GetNextUserId() => _nextUserId++;
        public int GetNextServiceId()
        {
            // При каждом вызове увеличиваем счетчик
            return _nextServiceId++;
        }
        public int GetNextShiftId() => _nextShiftId++;
        public int GetNextOrderId() => _nextOrderId++;
        public int GetNextAppointmentId() => _nextAppointmentId++; // Добавляем

        public void UpdateIds()
        {
            if (Users.Count > 0) _nextUserId = Users.Max(u => u.Id) + 1;
            if (Services.Count > 0) _nextServiceId = Services.Max(s => s.Id) + 1;
            if (Shifts.Count > 0) _nextShiftId = Shifts.Max(s => s.Id) + 1;
            if (Shifts.SelectMany(s => s.Orders).Any())
                _nextOrderId = Shifts.SelectMany(s => s.Orders).Max(o => o.Id) + 1;
            if (Appointments.Count > 0) _nextAppointmentId = Appointments.Max(a => a.Id) + 1;

            System.Diagnostics.Debug.WriteLine($"UpdateIds: NextServiceId = {_nextServiceId}");
        }
        public int GetNextClientId()
        {
            return Clients.Any() ? Clients.Max(c => c.Id) + 1 : 1;
        }
    }
}
