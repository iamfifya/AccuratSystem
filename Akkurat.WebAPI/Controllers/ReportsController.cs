using Accurat.WebAPI.Data;
using Accurat.WebAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Accurat.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public ReportsController(AppDbContext context) => _context = context;

        // ПАДАЛ ИЗ-ЗА ОТСУТСТВИЯ branchId В ФИЛЬТРЕ
        [HttpGet("shifts")]
        public async Task<ActionResult<IEnumerable<ShiftReport>>> GetShiftReports(int branchId, DateTime start, DateTime end)
        {
            var startUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(end, DateTimeKind.Utc).AddDays(1).AddTicks(-1);

            // ДОБАВЛЕН ФИЛЬТР s.BranchId == branchId
            var shifts = await _context.Shifts
                .Where(s => s.BranchId == branchId && s.IsClosed && s.Date >= startUtc && s.Date <= endUtc)
                .ToListAsync();

            var reports = new List<ShiftReport>();
            var allServices = await _context.Services.ToListAsync();
            // Убираем фильтрацию по BranchId, грузим всех пользователей для справочника имен
            var allUsers = await _context.Users.ToListAsync();

            foreach (var shift in shifts)
            {
                var orders = await _context.Orders.Where(o => o.ShiftId == shift.Id && o.Status == "Выполнен").ToListAsync();
                var transactions = await _context.Transactions.Where(t => t.ShiftId == shift.Id).ToListAsync();

                var report = new ShiftReport
                {
                    Id = shift.Id,
                    Date = shift.Date,
                    StartTime = shift.StartTime ?? shift.Date,
                    EndTime = shift.EndTime,
                    Notes = shift.Notes,
                    TotalCars = orders.Count,
                    TotalRevenue = orders.Sum(o => o.FinalPrice),
                    CashAmount = orders.Where(o => o.PaymentMethod == "Наличные").Sum(o => o.FinalPrice),
                    CashCount = orders.Count(o => o.PaymentMethod == "Наличные"),
                    CardAmount = orders.Where(o => o.PaymentMethod == "Карта").Sum(o => o.FinalPrice),
                    CardCount = orders.Count(o => o.PaymentMethod == "Карта"),
                    TransferAmount = orders.Where(o => o.PaymentMethod == "Перевод").Sum(o => o.FinalPrice),
                    TransferCount = orders.Count(o => o.PaymentMethod == "Перевод"),
                    QrAmount = orders.Where(o => o.PaymentMethod == "QR-код").Sum(o => o.FinalPrice),
                    QrCount = orders.Count(o => o.PaymentMethod == "QR-код"),
                    TotalExpenses = transactions.Where(t => t.Type == "Расход").Sum(t => t.Amount),
                    TotalAdvances = transactions.Where(t => t.Type == "Аванс мойщику").Sum(t => t.Amount)
                };

                decimal totalWasherEarnings = 0;
                decimal totalCompanyEarnings = 0;

                foreach (var group in orders.GroupBy(o => o.WasherId))
                {
                    if (group.Key == null || group.Key == 0) continue;

                    var emp = allUsers.FirstOrDefault(u => u.Id == group.Key);
                    decimal empRevenue = group.Sum(o => o.FinalPrice);

                    decimal empBaseEarnings = group.Sum(o => (o.OriginalTotalPrice + o.ExtraCost) * 0.35m);
                    decimal empTopUp = (empBaseEarnings > 0 && empBaseEarnings < 1000m) ? (1000m - empBaseEarnings) : 0;
                    decimal empTotalEarnings = empBaseEarnings + empTopUp;

                    decimal empAdvances = transactions.Where(t => t.EmployeeId == emp?.Id && t.Type == "Аванс мойщику").Sum(t => t.Amount);

                    totalWasherEarnings += empTotalEarnings;
                    totalCompanyEarnings += (empRevenue - empTotalEarnings);

                    report.EmployeesWork.Add(new EmployeeReport
                    {
                        EmployeeId = emp?.Id ?? 0,
                        EmployeeName = emp?.FullName ?? "Неизвестно",
                        CarsWashed = group.Count(),
                        TotalAmount = empRevenue,
                        Earnings = empTotalEarnings,
                        Advances = empAdvances
                    });
                }

                report.TotalWasherEarnings = totalWasherEarnings;
                report.TotalCompanyEarnings = totalCompanyEarnings;

                reports.Add(report);
            }

            return Ok(reports);
        }

        // ПАДАЛ ИЗ-ЗА ОТСУТСТВИЯ branchId В ПАРАМЕТРАХ
        [HttpGet("clients-stats")]
        public async Task<ActionResult<object>> GetClientsStats(int branchId, DateTime start, DateTime end)
        {
            var startUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(end, DateTimeKind.Utc).AddDays(1).AddTicks(-1);

            int newClients = await _context.Clients.CountAsync(c => c.RegistrationDate >= startUtc && c.RegistrationDate <= endUtc);

            // ДОБАВЛЕН ФИЛЬТР ПО ФИЛИАЛУ (o.BranchId == branchId)
            int uniqueClients = await _context.Orders
                .Where(o => o.BranchId == branchId && o.Time >= startUtc && o.Time <= endUtc && o.ClientId != null)
                .Select(o => o.ClientId)
                .Distinct()
                .CountAsync();

            return Ok(new { NewClients = newClients, UniqueClients = uniqueClients });
        }
    }
}