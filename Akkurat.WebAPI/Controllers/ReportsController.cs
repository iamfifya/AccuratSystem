using AccuratSystem.Contracts.Models;
using AccuratSystem.Contracts.Enums;
using AccuratSystem.Contracts.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Accurat.WebAPI.Data;
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

        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id) ? int.Parse(id) : 1;

        [HttpGet("shifts")]
        public async Task<ActionResult<IEnumerable<ShiftReport>>> GetShiftReports(int branchId, DateTime start, DateTime end)
        {
            var startUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(end, DateTimeKind.Utc).AddDays(1).AddTicks(-1);

            var shiftsQuery = _context.Shifts
                .Where(s => s.IsClosed && s.Date >= startUtc && s.Date <= endUtc);

            if (CurrentCompanyId != 0)
            {
                var myBranchIds = _context.Branches.Where(b => b.CompanyId == CurrentCompanyId).Select(b => b.Id);
                shiftsQuery = shiftsQuery.Where(s => myBranchIds.Contains(s.BranchId));
            }

            if (branchId > 0)
            {
                shiftsQuery = shiftsQuery.Where(s => s.BranchId == branchId);
            }

            var shifts = await shiftsQuery.ToListAsync();
            var reports = new List<ShiftReport>();

            var allUsers = await _context.Users.Where(u => CurrentCompanyId == 0 || u.CompanyId == CurrentCompanyId).ToListAsync();
            var allServices = await _context.Services.Where(s => CurrentCompanyId == 0 || s.CompanyId == CurrentCompanyId).ToListAsync();
            var settings = await _context.CompanySettings.FindAsync(CurrentCompanyId == 0 ? 1 : CurrentCompanyId);

            foreach (var shift in shifts)
            {
                var orders = await _context.Orders
                    .Include(o => o.OrderWashers)
                    .Where(o => o.ShiftId == shift.Id && (o.Status == "Выполнен" || o.Status == "Завершен")).ToListAsync();

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
                    TotalAdvances = transactions.Where(t => t.Type == "Аванс мойщику").Sum(t => t.Amount),
                    WashTotalCars = orders.Count(o => o.Department == "Wash"),
                    WashTotalRevenue = orders.Where(o => o.Department == "Wash").Sum(o => o.FinalPrice),
                    WashTotalExpenses = transactions.Where(t => t.Type == "Расход" && t.Department == "Wash").Sum(t => t.Amount),
                    ServiceTotalCars = orders.Count(o => o.Department == "Service"),
                    ServiceTotalRevenue = orders.Where(o => o.Department == "Service").Sum(o => o.FinalPrice),
                    ServiceTotalExpenses = transactions.Where(t => t.Type == "Расход" && t.Department == "Service").Sum(t => t.Amount)
                };

                decimal totalFOT = 0;
                var shiftEmployeeIds = shift.EmployeeIds?.ToList() ?? new List<int>();
                var washerIds = orders.SelectMany(o => o.OrderWashers?.Select(ow => ow.UserId) ?? new List<int>());
                var allEmployeeIds = shiftEmployeeIds.Union(washerIds).Distinct().ToList();

                foreach (var empId in allEmployeeIds)
                {
                    if (empId == 0) continue;
                    var emp = allUsers.FirstOrDefault(u => u.Id == empId);
                    if (emp == null) continue;

                    decimal empEarnings = 0;
                    decimal empAdvances = transactions.Where(t => t.EmployeeId == emp.Id && t.Type == "Аванс мойщику").Sum(t => t.Amount);

                    if (emp.RoleId == 3 || emp.RoleId == 4) // Мойщики и Сервис: Берем ЗАМОРОЖЕННЫЕ данные
                    {
                        empEarnings = await _context.OrderWashers
                            .Where(ow => ow.UserId == empId &&
                                         _context.Orders.Any(o => o.Id == ow.OrderId && o.ShiftId == shift.Id))
                            .SumAsync(ow => ow.EarnedAmount);
                    }
                    else // Админы: расчет живой (от оборота смены)
                    {
                        var adminStats = OrderMath.CalculateShiftStats(orders, allServices, emp, shift.Type, allUsers, settings);
                        empEarnings = adminStats.TotalEarned;
                    }

                    report.EmployeesWork.Add(new EmployeeReport
                    {
                        EmployeeId = emp.Id,
                        EmployeeName = emp.FullName,
                        Earnings = empEarnings,
                        Advances = empAdvances
                    });
                    totalFOT += empEarnings;
                }

                report.TotalWasherEarnings = totalFOT;
                report.TotalCompanyEarnings = report.TotalRevenue - totalFOT;
                reports.Add(report);
            }

            return Ok(reports);
        }

        [HttpGet("clients-stats")]
        public async Task<ActionResult<object>> GetClientsStats(int branchId, DateTime start, DateTime end)
        {
            var startUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(end, DateTimeKind.Utc).AddDays(1).AddTicks(-1);
            var newClientsQuery = _context.Clients.Where(c => c.RegistrationDate >= startUtc && c.RegistrationDate <= endUtc);
            var uniqueClientsQuery = _context.Orders.Where(o => o.Time >= startUtc && o.Time <= endUtc && o.ClientId != null);

            if (CurrentCompanyId != 0)
            {
                newClientsQuery = newClientsQuery.Where(c => c.CompanyId == CurrentCompanyId);
                var myBranchIds = _context.Branches.Where(b => b.CompanyId == CurrentCompanyId).Select(b => b.Id);
                uniqueClientsQuery = uniqueClientsQuery.Where(o => myBranchIds.Contains(o.BranchId));
            }

            if (branchId > 0) uniqueClientsQuery = uniqueClientsQuery.Where(o => o.BranchId == branchId);

            int newClients = await newClientsQuery.CountAsync();
            int uniqueClients = await uniqueClientsQuery.Select(o => o.ClientId).Distinct().CountAsync();

            return Ok(new { NewClients = newClients, UniqueClients = uniqueClients });
        }
    }
}
