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

        // ЧИТАЕМ ЗАГОЛОВОК ТЕНАНТА
        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id) ? int.Parse(id) : 1;

        private decimal ExtractUpsellBonus(string notes)
        {
            if (string.IsNullOrEmpty(notes) || !notes.Contains("бонус: +")) return 0;
            try
            {
                int startIndex = notes.IndexOf("бонус: +") + "бонус: +".Length;
                int endIndex = notes.IndexOf(" ₽", startIndex);
                if (endIndex > startIndex)
                {
                    string bonusStr = notes.Substring(startIndex, endIndex - startIndex).Trim();
                    if (decimal.TryParse(bonusStr, out decimal bonus)) return bonus;
                }
            }
            catch { }
            return 0;
        }

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

            // ДОБАВЛЕНО: Загружаем настройки компании для расчетов ЗП
            var settings = await _context.CompanySettings.FindAsync(CurrentCompanyId == 0 ? 1 : CurrentCompanyId);

            foreach (var shift in shifts)
            {
                var orders = await _context.Orders
                    .Include(o => o.OrderWashers)
                        .ThenInclude(ow => ow.Washer)
                    .Where(o => o.ShiftId == shift.Id && (o.Status == "Выполнен" || o.Status == "Завершен")).ToListAsync();
                var transactions = await _context.Transactions.Where(t => t.ShiftId == shift.Id).ToListAsync();

                var washOrders = orders.Where(o => o.Department == "Wash").ToList();
                var serviceOrders = orders.Where(o => o.Department == "Service").ToList();

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
                    WashTotalCars = washOrders.Count,
                    WashTotalRevenue = washOrders.Sum(o => o.FinalPrice),
                    WashTotalExpenses = transactions.Where(t => t.Type == "Расход" && t.Department == "Wash").Sum(t => t.Amount),
                    ServiceTotalCars = serviceOrders.Count,
                    ServiceTotalRevenue = serviceOrders.Sum(o => o.FinalPrice),
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

                    decimal empTotalEarnings = 0;
                    decimal empRevenue = 0;
                    int carsProcessed = 0;

                    if (emp.RoleId == 3 || emp.RoleId == 4)
                    {
                        var myTasks = orders.SelectMany(o => o.OrderWashers.Where(ow => ow.UserId == empId), (o, ow) => new { Order = o, OrderWasher = ow }).ToList();
                        carsProcessed = myTasks.Count;
                        empRevenue = myTasks.Sum(x => x.Order.FinalPrice * x.OrderWasher.SplitShare);

                        // ИСПРАВЛЕНО: Передаем тип смены (shift.Type) и настройки (settings)
                        empTotalEarnings = myTasks.Sum(x => Accurat.WebAPI.Services.SalaryCalculationService.CalculateWasherIncomeForOrder(x.OrderWasher, x.Order, allServices, allUsers, shift.Type, settings));
                    }
                    else
                    {
                        carsProcessed = orders.Count;
                        empRevenue = report.TotalRevenue;
                        decimal baseSalary = emp.BaseSalaryPerShift;
                        decimal percentEarnings = empRevenue * (emp.BaseWagePercentage / 100m);
                        decimal personalUpsellBonus = orders.Where(o => o.AdminId == emp.Id).Sum(o => ExtractUpsellBonus(o.Notes));
                        empTotalEarnings = baseSalary + percentEarnings + personalUpsellBonus;
                    }

                    decimal empAdvances = transactions.Where(t => t.EmployeeId == emp.Id && t.Type == "Аванс мойщику").Sum(t => t.Amount);
                    totalFOT += empTotalEarnings;

                    report.EmployeesWork.Add(new EmployeeReport
                    {
                        EmployeeId = emp.Id,
                        EmployeeName = emp.FullName,
                        CarsWashed = carsProcessed,
                        TotalAmount = empRevenue,
                        Earnings = empTotalEarnings,
                        Advances = empAdvances
                    });
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

            // БРОНЕЖИЛЕТ
            if (CurrentCompanyId != 0)
            {
                newClientsQuery = newClientsQuery.Where(c => c.CompanyId == CurrentCompanyId);

                var myBranchIds = _context.Branches.Where(b => b.CompanyId == CurrentCompanyId).Select(b => b.Id);
                uniqueClientsQuery = uniqueClientsQuery.Where(o => myBranchIds.Contains(o.BranchId));
            }

            if (branchId > 0)
            {
                uniqueClientsQuery = uniqueClientsQuery.Where(o => o.BranchId == branchId);
            }

            int newClients = await newClientsQuery.CountAsync();
            int uniqueClients = await uniqueClientsQuery.Select(o => o.ClientId).Distinct().CountAsync();

            return Ok(new { NewClients = newClients, UniqueClients = uniqueClients });
        }
    }
}