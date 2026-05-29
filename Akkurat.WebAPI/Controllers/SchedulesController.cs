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

        [HttpGet("{branchId}/{year}/{month}")]
        public async Task<ActionResult<List<EmployeeScheduleDto>>> GetSchedule(int branchId, int year, int month)
        {
            // 1. Получаем сотрудников + СРАЗУ ПОДТЯГИВАЕМ ИХ РОЛИ ИЗ НОВОЙ ТАБЛИЦЫ
            var users = await _context.Users
                .Include(u => u.Role) // ✅ Инклудим навигационное свойство (сам класс Role)
                .Where(u => u.IsActive && (u.BranchId == branchId || u.RoleId == (int)UserRole.Director))
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

                    // Берем реальное имя из таблицы Roles. Если Role по какой-то причине null, пишем "Сотрудник"
                    Position = u.Role != null ? u.Role.Name : "Сотрудник",

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
                        BranchId = branchId,
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