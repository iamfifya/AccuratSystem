using AccuratSystem.Contracts.Models;
using AccuratSystem.Contracts.Enums;
using AccuratSystem.Contracts.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Accurat.WebAPI.Data;

namespace Accurat.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SchedulesController : ControllerBase
    {
        private readonly AppDbContext _context;
        public SchedulesController(AppDbContext context) => _context = context;

        // ДОБАВЛЯЕМ branchId в путь: api/Schedules/1/2024/10
        [HttpGet("{branchId}/{year}/{month}")]
        public async Task<ActionResult<List<EmployeeScheduleDto>>> GetSchedule(int branchId, int year, int month)
        {
            // 1. Получаем только тех сотрудников, которые привязаны к этому филиалу
            // Либо тех, кто вообще активен (если директор хочет видеть всех)
            var users = await _context.Users
                .Where(u => u.IsActive && (u.BranchId == branchId || u.Role == 1))
                .ToListAsync();

            var entries = await _context.EmployeeSchedules
                .Where(e => e.BranchId == branchId && e.Year == year && e.Month == month)
                .ToListAsync();

            var result = new List<EmployeeScheduleDto>();
            foreach (var u in users)
            {
                var empSch = new EmployeeScheduleDto
                {
                    EmployeeId = u.Id,
                    EmployeeName = u.FullName,
                    // Используем вашу новую логику ролей
                    Position = u.Role switch
                    {
                        1 => "Директор",
                        2 => "Администратор",
                        3 => "Сотрудник сервиса",
                        4 => "Мойщик",
                        _ => "Сотрудник"
                    },
                    Days = new Dictionary<int, string>()
                };

                for (int day = 1; day <= DateTime.DaysInMonth(year, month); day++)
                {
                    var status = entries.FirstOrDefault(e => e.EmployeeId == u.Id && e.Day == day)?.Status;
                    empSch.Days[day] = status ?? "";
                }
                result.Add(empSch);
            }
            return Ok(result);
        }

        [HttpPost("{branchId}/{year}/{month}")]
        public async Task<IActionResult> SaveSchedule(int branchId, int year, int month, [FromBody] List<EmployeeScheduleDto> scheduleData)
        {
            // Удаляем старые записи ТОЛЬКО для этого филиала
            var oldEntries = await _context.EmployeeSchedules
                .Where(e => e.BranchId == branchId && e.Year == year && e.Month == month)
                .ToListAsync();

            _context.EmployeeSchedules.RemoveRange(oldEntries);

            foreach (var emp in scheduleData)
            {
                foreach (var kv in emp.Days)
                {
                    if (string.IsNullOrEmpty(kv.Value)) continue;

                    _context.EmployeeSchedules.Add(new EmployeeScheduleEntry
                    {
                        EmployeeId = emp.EmployeeId,
                        BranchId = branchId, // ТЕПЕРЬ ПЕРЕДАЕМ BranchId в базу!
                        Year = year,
                        Month = month,
                        Day = kv.Key,
                        Status = kv.Value
                    });
                }
            }
            await _context.SaveChangesAsync();
            return Ok();
        }
    }


    public class EmployeeScheduleDto
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public Dictionary<int, string> Days { get; set; } = new Dictionary<int, string>();
    }
}