// Akkurat.WebAPI/Models/EmployeeScheduleEntry.cs
namespace Accurat.WebAPI.Models
{
    public class EmployeeScheduleEntry
    {
        public int EmployeeId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public int Day { get; set; }
        public string Status { get; set; } = string.Empty;

        public User? Employee { get; set; }
    }
}