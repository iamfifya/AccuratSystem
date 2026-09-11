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
            if (!shifts.Any()) return Ok(reports);

            var shiftIds = shifts.Select(s => s.Id).ToList();

            var allShiftOrders = await _context.Orders
                .Include(o => o.OrderWashers)
                .Include(o => o.OrderServiceItems)   // для топ-услуг
                .Where(o => shiftIds.Contains(o.ShiftId) && (o.Status == "Выполнен" || o.Status == "Завершен"))
                .ToListAsync();

            var allShiftTransactions = await _context.Transactions
                .Where(t => t.ShiftId.HasValue && shiftIds.Contains(t.ShiftId.Value))
                .ToListAsync();

            var allUsers = await _context.Users.Where(u => CurrentCompanyId == 0 || u.CompanyId == CurrentCompanyId).ToListAsync();
            var allServices = await _context.Services.Where(s => CurrentCompanyId == 0 || s.CompanyId == CurrentCompanyId).ToListAsync();
            var settings = await _context.CompanySettings.FindAsync(CurrentCompanyId == 0 ? 1 : CurrentCompanyId);

            foreach (var shift in shifts)
            {
                var orders = allShiftOrders.Where(o => o.ShiftId == shift.Id).ToList();
                var transactions = allShiftTransactions.Where(t => t.ShiftId == shift.Id).ToList();

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

                // === ФОТ СОТРУДНИКОВ (мойщики + админы) ===
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

                    var empOrderWashers = orders
                        .SelectMany(o => (o.OrderWashers ?? new List<OrderWasher>())
                            .Select(ow => new { Order = o, Washer = ow }))
                        .Where(x => x.Washer.UserId == empId)
                        .ToList();

                    if (emp.RoleId == 3 || emp.RoleId == 4) // МОЙЩИКИ И СЕРВИС
                    {
                        empEarnings = empOrderWashers.Sum(x => x.Washer.EarnedAmount);
                    }
                    else // АДМИНЫ И ДИРЕКТОРА
                    {
                        if (shift.IsClosed && shift.AdminEarningsSnapshot > 0)
                        {
                            var adminsInShiftCount = shift.EmployeeIds?.Count(uid =>
                                allUsers.FirstOrDefault(u => u.Id == uid)?.RoleId == 1 ||
                                allUsers.FirstOrDefault(u => u.Id == uid)?.RoleId == 2) ?? 1;
                            empEarnings = shift.AdminEarningsSnapshot / (adminsInShiftCount > 0 ? adminsInShiftCount : 1);
                        }
                        else
                        {
                            var adminStats = OrderMath.CalculateShiftStats(orders, allServices, emp, shift.Type, allUsers, settings);
                            empEarnings = adminStats.TotalEarned;
                        }
                    }

                    report.EmployeesWork.Add(new EmployeeReport
                    {
                        EmployeeId = emp.Id,
                        EmployeeName = emp.FullName,
                        CarsWashed = empOrderWashers.Count,
                        TotalAmount = empOrderWashers.Sum(x => x.Order.FinalPrice),
                        Earnings = empEarnings,
                        Advances = empAdvances
                    });
                    totalFOT += empEarnings;
                }

                // === ИТОГИ СМЕНЫ ===
                report.TotalWasherEarnings = totalFOT;
                report.TotalCompanyEarnings = report.TotalRevenue - totalFOT;

                // === РАСКЛАДКА ФОТ АДМИНОВ ПО ДЕПАРТАМЕНТАМ ===
                // washWasherFOT — замороженные зарплаты мойщиков департамента Wash
                decimal washWasherFOT = orders.Where(o => o.Department == "Wash")
                    .SelectMany(o => o.OrderWashers ?? new List<OrderWasher>())
                    .Sum(ow => ow.EarnedAmount);

                // serviceWasherFOT — то же самое по департаменту Service
                decimal serviceWasherFOT = orders.Where(o => o.Department == "Service")
                    .SelectMany(o => o.OrderWashers ?? new List<OrderWasher>())
                    .Sum(ow => ow.EarnedAmount);

                // adminFOT — зарплата админов/директоров за смену.
                // Считаем как разницу: весь ФОТ минус ФОТ мойщиков
                decimal adminFOT = totalFOT - (washWasherFOT + serviceWasherFOT);

                // washShare / serviceShare — доля департамента в общей кассе смены.
                // По ним пропорционально раскладываем админский ФОТ
                decimal washShare = report.TotalRevenue > 0 ? report.WashTotalRevenue / report.TotalRevenue : 0m;
                decimal serviceShare = report.TotalRevenue > 0 ? report.ServiceTotalRevenue / report.TotalRevenue : 0m;

                // WashCompanyEarnings — прибыль мойки ПОСЛЕ всех зарплат:
                // касса мойки − мойщики мойки − доля админского ФОТ
                report.WashCompanyEarnings = report.WashTotalRevenue - washWasherFOT - (adminFOT * washShare);

                // ServiceCompanyEarnings — то же самое по сервису
                report.ServiceCompanyEarnings = report.ServiceTotalRevenue - serviceWasherFOT - (adminFOT * serviceShare);

                // === БЫСТРЫЕ МЕТРИКИ ===

                // Средний чек
                report.AverageCheck = report.TotalCars > 0
                    ? report.TotalRevenue / report.TotalCars
                    : 0;

                // Аналитика по скидкам
                report.TotalDiscountAmount = orders.Sum(o =>
                    o.DiscountPercent > 0
                        ? o.TotalPrice * o.DiscountPercent / 100
                        : o.DiscountAmount);
                report.DiscountedOrdersCount = orders.Count(o => o.DiscountPercent > 0 || o.DiscountAmount > 0);

                // Топ услуг за смену (по snapshot-ценам из OrderServiceItems)
                var serviceStats = new Dictionary<int, ServiceAnalytics>();
                foreach (var order in orders)
                {
                    if (order.OrderServiceItems == null) continue;
                    foreach (var item in order.OrderServiceItems)
                    {
                        if (!serviceStats.ContainsKey(item.ServiceId))
                        {
                            var svc = allServices.FirstOrDefault(s => s.Id == item.ServiceId);
                            serviceStats[item.ServiceId] = new ServiceAnalytics
                            {
                                ServiceName = svc?.Name ?? $"Услуга #{item.ServiceId}",
                                Count = 0,
                                TotalRevenue = 0
                            };
                        }
                        serviceStats[item.ServiceId].Count++;
                        serviceStats[item.ServiceId].TotalRevenue += item.ActualPrice * item.Quantity;
                    }
                }
                report.TopServices = serviceStats.Values
                    .OrderByDescending(s => s.Count)
                    .ThenByDescending(s => s.TotalRevenue)
                    .Take(5)
                    .ToList();

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

            // === НОВОЕ: возвращаемость клиентов ===
            var clientVisitCounts = await uniqueClientsQuery
                .GroupBy(o => o.ClientId)
                .Select(g => new { ClientId = g.Key, VisitsCount = g.Count() })
                .ToListAsync();

            int uniqueClients = clientVisitCounts.Count;
            int repeatClients = clientVisitCounts.Count(c => c.VisitsCount > 1);
            decimal retentionRate = uniqueClients > 0
                ? Math.Round((decimal)repeatClients / uniqueClients * 100, 1)
                : 0;

            return Ok(new
            {
                NewClients = newClients,
                UniqueClients = uniqueClients,
                RepeatClients = repeatClients,      // НОВОЕ
                RetentionRate = retentionRate        // НОВОЕ
            });
        }

        [HttpGet("reconciliations-summary")]
        public async Task<ActionResult<object>> GetReconciliationsSummary(int branchId, DateTime start, DateTime end)
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

            var shiftIds = await shiftsQuery.Select(s => s.Id).ToListAsync();
            if (!shiftIds.Any()) return Ok(new { TotalShifts = 0, ReconciledShifts = 0, TotalDifference = 0m });

            var reconciliations = await _context.CashReconciliations
                .Where(r => shiftIds.Contains(r.ShiftId))
                .ToListAsync();

            // Берём только последний пересчёт для каждой смены (если их было несколько)
            var grouped = reconciliations
                .GroupBy(r => r.ShiftId)
                .Select(g => g.OrderByDescending(r => r.CountedAt).First())
                .ToList();

            return Ok(new
            {
                TotalShifts = shiftIds.Count,
                ReconciledShifts = grouped.Count,
                TotalDifference = grouped.Sum(r => r.Difference)
            });
        }
    }
}
