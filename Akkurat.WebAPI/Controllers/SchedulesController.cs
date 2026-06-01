using AccuratSystem.Contracts.Models;
using AccuratSystem.Contracts.Enums;
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

        // Читаем заголовок компании!
        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id) ? int.Parse(id) : 1;

        [HttpGet("{branchId}/{year}/{month}")]
        public async Task<ActionResult<List<EmployeeSchedule>>> GetSchedule(int branchId, int year, int month)
        {
            // Жестко фильтруем юзеров по CompanyId
            var users = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.IsActive &&
                            (CurrentCompanyId == 0 || u.CompanyId == CurrentCompanyId) && // Изоляция SaaS
                            (u.BranchId == branchId || u.RoleId == 1 || u.RoleId == 2))   // Либо привязан к филиалу, либо Директор/Управляющий
                .ToListAsync();

            var entries = await _context.EmployeeSchedules
                .Where(e => e.BranchId == branchId && e.Year == year && e.Month == month)
                .ToListAsync();

            // ИСПОЛЬЗУЕМ КЛАСС ИЗ КОНТРАКТОВ
            var result = new List<EmployeeSchedule>();
            foreach (var u in users)
            {
                var empSch = new EmployeeSchedule
                {
                    EmployeeId = u.Id,
                    EmployeeName = u.FullName,
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
        public async Task<IActionResult> SaveSchedule(int branchId, int year, int month, [FromBody] List<EmployeeSchedule> scheduleData)
        {
            // ЗАЩИТА: Нельзя сохранять графики в чужие филиалы
            if (CurrentCompanyId != 0)
            {
                var branch = await _context.Branches.FindAsync(branchId);
                if (branch == null || branch.CompanyId != CurrentCompanyId) return Forbid();
            }

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
}