using Accurat.WebAPI.Data;
using Accurat.WebAPI.Time;
using AccuratSystem.Contracts.DTOs;
using AccuratSystem.Contracts.Enums;
using AccuratSystem.Contracts.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;

namespace Accurat.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly Accurat.WebAPI.Time.IBranchZoneResolver _zoneResolver;

        public ReportsController(AppDbContext context, Accurat.WebAPI.Time.IBranchZoneResolver zoneResolver)
        {
            _context = context;
            _zoneResolver = zoneResolver;
        }
        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id) ? int.Parse(id) : 1;

        [HttpGet("shifts")]
        public async Task<ActionResult<IEnumerable<ShiftReport>>> GetShiftReports(int branchId, DateTime start, DateTime end)
        {

            // Определяем часовую зону филиала (или зону по умолчанию)
            var zone = branchId > 0
                    ? _zoneResolver.GetZone(branchId)
                    : _zoneResolver.DefaultZone;

            var (startUtc, endUtc) = BusinessTime.StoredRangeInclusive(start, end, zone);

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
                .Include(o => o.OrderServiceItems)
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

                // === ФОТ СОТРУДНИКОВ ===
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

                    if (emp.RoleId == 3 || emp.RoleId == 4)
                    {
                        empEarnings = empOrderWashers.Sum(x => x.Washer.EarnedAmount);
                    }
                    else
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

                report.TotalWasherEarnings = totalFOT;
                report.TotalCompanyEarnings = report.TotalRevenue - totalFOT;

                decimal washWasherFOT = orders.Where(o => o.Department == "Wash")
                    .SelectMany(o => o.OrderWashers ?? new List<OrderWasher>())
                    .Sum(ow => ow.EarnedAmount);
                decimal serviceWasherFOT = orders.Where(o => o.Department == "Service")
                    .SelectMany(o => o.OrderWashers ?? new List<OrderWasher>())
                    .Sum(ow => ow.EarnedAmount);
                decimal adminFOT = totalFOT - (washWasherFOT + serviceWasherFOT);

                decimal washShare = report.TotalRevenue > 0 ? report.WashTotalRevenue / report.TotalRevenue : 0m;
                decimal serviceShare = report.TotalRevenue > 0 ? report.ServiceTotalRevenue / report.TotalRevenue : 0m;

                report.WashCompanyEarnings = report.WashTotalRevenue - washWasherFOT - (adminFOT * washShare);
                report.ServiceCompanyEarnings = report.ServiceTotalRevenue - serviceWasherFOT - (adminFOT * serviceShare);

                report.AverageCheck = report.TotalCars > 0 ? report.TotalRevenue / report.TotalCars : 0;
                report.TotalDiscountAmount = orders.Sum(o =>
                    o.DiscountPercent > 0 ? o.TotalPrice * o.DiscountPercent / 100 : o.DiscountAmount);
                report.DiscountedOrdersCount = orders.Count(o => o.DiscountPercent > 0 || o.DiscountAmount > 0);

                // === РАЗБИВКА РАСХОДОВ ПО КАТЕГОРИЯМ ===
                var expenses = transactions.Where(t => t.Type == "Расход").ToList();
                var expenseGroups = expenses.GroupBy(t => string.IsNullOrWhiteSpace(t.Comment) ? "Без категории" : t.Comment)
                    .Select(g => new ExpenseCategoryReport
                    {
                        Category = g.Key,
                        TotalAmount = g.Sum(t => t.Amount),
                        Count = g.Count(),
                        Percentage = expenses.Sum(t => t.Amount) > 0
                        ? Math.Round(g.Sum(t => t.Amount) / expenses.Sum(t => t.Amount) * 100, 1)
                        : 0
                    })
                    .OrderByDescending(e => e.TotalAmount)
                    .ToList();
                report.ExpensesByCategory = expenseGroups;

                // === ЗАГРУЖЕННОСТЬ ПО ЧАСАМ ===
                var hourlyLoad = new List<HourlyLoad>();
                for (int hour = 0; hour < 24; hour++)
                {
                    var hourOrders = orders.Where(o => o.Time.BusinessHour(zone) == hour).ToList();
                    hourlyLoad.Add(new HourlyLoad
                    {
                        Hour = hour,
                        HourLabel = $"{hour:D2}:00",
                        OrdersCount = hourOrders.Count,
                        Revenue = hourOrders.Sum(o => o.FinalPrice)
                    });
                }
                report.HourlyLoad = hourlyLoad;

                // === ОСТАТОК ПО КАССЕ ===
                var cashTransactions = transactions.Where(t => t.Type == "Приход" || t.Type == "Расход" || t.Type == "Инкассация").ToList();
                var expectedCash = cashTransactions
                    .Where(t => t.Type == "Приход")
                    .Sum(t => t.Amount)
                    - cashTransactions.Where(t => t.Type == "Расход").Sum(t => t.Amount)
                    - cashTransactions.Where(t => t.Type == "Инкассация").Sum(t => t.Amount);

                var lastReconciliation = await _context.CashReconciliations
                    .Where(r => r.ShiftId == shift.Id)
                    .OrderByDescending(r => r.CountedAt)
                    .FirstOrDefaultAsync();

                report.ExpectedCashBalance = expectedCash;
                report.ActualCashBalance = lastReconciliation?.ActualCash ?? 0;
                report.CashBalanceDifference = lastReconciliation?.Difference ?? 0;

                // Топ услуг
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

        // === НОВЫЙ ЭНДПОИНТ: СРАВНЕНИЕ ДВУХ ПЕРИОДОВ ===
        [HttpGet("compare-periods")]
        public async Task<ActionResult<PeriodComparison>> ComparePeriods(
            int branchId,
            DateTime currentStart, DateTime currentEnd,
            DateTime previousStart, DateTime previousEnd)
        {
            var currentReports = await GetShiftReportsInternal(branchId, currentStart, currentEnd);
            var previousReports = await GetShiftReportsInternal(branchId, previousStart, previousEnd);

            if (!currentReports.Any() && !previousReports.Any())
                return NotFound("Нет данных для указанных периодов");

            var current = AggregateReports(currentReports);
            var previous = AggregateReports(previousReports);

            var comparison = new PeriodComparison
            {
                CurrentPeriod = current,
                PreviousPeriod = previous,

                RevenueChange = current.TotalRevenue - previous.TotalRevenue,
                RevenueChangePercent = previous.TotalRevenue > 0
                    ? Math.Round((current.TotalRevenue - previous.TotalRevenue) / previous.TotalRevenue * 100, 1)
                    : 0,

                ProfitChange = current.NetProfit - previous.NetProfit,
                ProfitChangePercent = previous.NetProfit > 0
                    ? Math.Round((current.NetProfit - previous.NetProfit) / previous.NetProfit * 100, 1)
                    : 0,

                CarsChange = current.TotalCars - previous.TotalCars,
                CarsChangePercent = previous.TotalCars > 0
                    ? Math.Round((decimal)(current.TotalCars - previous.TotalCars) / previous.TotalCars * 100, 1)
                    : 0,

                AvgCheckChange = current.AverageCheck - previous.AverageCheck,
                AvgCheckChangePercent = previous.AverageCheck > 0
                    ? Math.Round((current.AverageCheck - previous.AverageCheck) / previous.AverageCheck * 100, 1)
                    : 0
            };

            return Ok(comparison);
        }

        private async Task<List<ShiftReport>> GetShiftReportsInternal(int branchId, DateTime start, DateTime end)
        {
            var result = await GetShiftReports(branchId, start, end);
            if (result.Result is OkObjectResult okResult)
            {
                return okResult.Value as List<ShiftReport> ?? new List<ShiftReport>();
            }
            return new List<ShiftReport>();
        }

        private BaseReport AggregateReports(List<ShiftReport> reports)
        {
            if (!reports.Any()) return new BaseReport();

            var aggregated = new BaseReport
            {
                TotalCars = reports.Sum(r => r.TotalCars),
                TotalRevenue = reports.Sum(r => r.TotalRevenue),
                TotalWasherEarnings = reports.Sum(r => r.TotalWasherEarnings),
                TotalCompanyEarnings = reports.Sum(r => r.TotalCompanyEarnings),

                WashTotalCars = reports.Sum(r => r.WashTotalCars),
                WashTotalRevenue = reports.Sum(r => r.WashTotalRevenue),
                WashCompanyEarnings = reports.Sum(r => r.WashCompanyEarnings),
                WashTotalExpenses = reports.Sum(r => r.WashTotalExpenses),

                ServiceTotalCars = reports.Sum(r => r.ServiceTotalCars),
                ServiceTotalRevenue = reports.Sum(r => r.ServiceTotalRevenue),
                ServiceCompanyEarnings = reports.Sum(r => r.ServiceCompanyEarnings),
                ServiceTotalExpenses = reports.Sum(r => r.ServiceTotalExpenses),

                CashCount = reports.Sum(r => r.CashCount),
                CashAmount = reports.Sum(r => r.CashAmount),
                CardCount = reports.Sum(r => r.CardCount),
                CardAmount = reports.Sum(r => r.CardAmount),
                TransferCount = reports.Sum(r => r.TransferCount),
                TransferAmount = reports.Sum(r => r.TransferAmount),
                QrCount = reports.Sum(r => r.QrCount),
                QrAmount = reports.Sum(r => r.QrAmount),

                TotalExpenses = reports.Sum(r => r.TotalExpenses),
                TotalAdvances = reports.Sum(r => r.TotalAdvances),
                TotalDiscountAmount = reports.Sum(r => r.TotalDiscountAmount),
                DiscountedOrdersCount = reports.Sum(r => r.DiscountedOrdersCount)
            };

            var allExpenses = reports.SelectMany(r => r.ExpensesByCategory).ToList();
            aggregated.ExpensesByCategory = allExpenses
                .GroupBy(e => e.Category)
                .Select(g => new ExpenseCategoryReport
                {
                    Category = g.Key,
                    TotalAmount = g.Sum(e => e.TotalAmount),
                    Count = g.Sum(e => e.Count),
                    Percentage = allExpenses.Sum(e => e.TotalAmount) > 0
                    ? Math.Round(g.Sum(e => e.TotalAmount) / allExpenses.Sum(e => e.TotalAmount) * 100, 1)
                        : 0
                })
                .OrderByDescending(e => e.TotalAmount)
                .ToList();

            var allHourlyLoad = reports.SelectMany(r => r.HourlyLoad).ToList();
            aggregated.HourlyLoad = Enumerable.Range(0, 24)
                .Select(hour => new HourlyLoad
                {
                    Hour = hour,
                    HourLabel = $"{hour:D2}:00",
                    OrdersCount = allHourlyLoad.Where(h => h.Hour == hour).Sum(h => h.OrdersCount),
                    Revenue = allHourlyLoad.Where(h => h.Hour == hour).Sum(h => h.Revenue)
                })
                .ToList();

            var allServices = reports.SelectMany(r => r.TopServices).ToList();
            aggregated.TopServices = allServices
                .GroupBy(s => s.ServiceName)
                .Select(g => new ServiceAnalytics
                {
                    ServiceName = g.Key,
                    Count = g.Sum(s => s.Count),
                    TotalRevenue = g.Sum(s => s.TotalRevenue)
                })
                .OrderByDescending(s => s.Count)
                .ThenByDescending(s => s.TotalRevenue)
                .Take(5)
                .ToList();

            // === Агрегация сотрудников по всем сменам периода ===
            aggregated.EmployeesWork = reports
                .SelectMany(r => r.EmployeesWork)
                .GroupBy(e => e.EmployeeId)
                .Select(g => new EmployeeReport
                {
                    EmployeeId = g.Key,
                    EmployeeName = g.First().EmployeeName,
                    CarsWashed = g.Sum(x => x.CarsWashed),
                    TotalAmount = g.Sum(x => x.TotalAmount),
                    Earnings = g.Sum(x => x.Earnings),
                    Advances = g.Sum(x => x.Advances)
                })
                .OrderByDescending(e => e.Earnings)
                .ToList();

            aggregated.AverageCheck = aggregated.TotalCars > 0
                ? aggregated.TotalRevenue / aggregated.TotalCars
                : 0;

            aggregated.ExpectedCashBalance = reports.Average(r => r.ExpectedCashBalance);
            aggregated.ActualCashBalance = reports.Average(r => r.ActualCashBalance);
            aggregated.CashBalanceDifference = reports.Sum(r => r.CashBalanceDifference);

            return aggregated;
        }

        [HttpGet("clients-stats")]
        public async Task<ActionResult<object>> GetClientsStats(int branchId, DateTime start, DateTime end)
        {

            // Определяем часовую зону филиала (или зону по умолчанию)
            var zone = branchId > 0 ? _zoneResolver.GetZone(branchId) : _zoneResolver.DefaultZone;
            var (startUtc, endUtc) = BusinessTime.StoredRangeInclusive(start, end, zone);

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
                RepeatClients = repeatClients,
                RetentionRate = retentionRate
            });
        }

        [HttpGet("reconciliations-summary")]
        public async Task<ActionResult<object>> GetReconciliationsSummary(int branchId, DateTime start, DateTime end)
        {

            // Определяем часовую зону филиала (или зону по умолчанию)
            var zone = branchId > 0
                    ? _zoneResolver.GetZone(branchId)
                    : _zoneResolver.DefaultZone;

            var (startUtc, endUtc) = BusinessTime.StoredRangeInclusive(start, end, zone);

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

        // ═══════════════════════════════════════════════════════
        //  ПОЛНОЕ СРАВНЕНИЕ ДВУХ ПЕРИОДОВ
        // ═══════════════════════════════════════════════════════

        [HttpGet("compare-periods-full")]
        public async Task<ActionResult<PeriodComparisonFull>> ComparePeriodsFull(
            int branchId,
            DateTime currentStart, DateTime currentEnd,
            DateTime previousStart, DateTime previousEnd)
        {
            // Зону резолвим внутри через резолвер
            var zone = branchId > 0 ? _zoneResolver.GetZone(branchId) : _zoneResolver.DefaultZone;

            Console.WriteLine("═══════════════════════════════════════════");
            Console.WriteLine($"ComparePeriodsFull CALLED: branch={branchId}, zone={zone.Id}");
            Console.WriteLine("═══════════════════════════════════════════");

            var result = new PeriodComparisonFull();

            // 1. Получаем отчёты за оба периода
            var currentReports = await GetShiftReportsInternal(branchId, currentStart, currentEnd);
            var previousReports = await GetShiftReportsInternal(branchId, previousStart, previousEnd);

            // 2. Проверяем наличие данных
            result.HasCurrentData = currentReports.Any();
            result.HasPreviousData = previousReports.Any();

            if (!result.HasCurrentData && !result.HasPreviousData)
            {
                result.Warning = "В обоих периодах нет данных для сравнения.";
                return Ok(result);
            }
            else if (!result.HasCurrentData)
            {
                result.Warning = $"В текущем периоде ({currentStart:dd.MM} - {currentEnd:dd.MM}) нет данных. Сравнение может быть некорректным.";
            }
            else if (!result.HasPreviousData)
            {
                result.Warning = $"В прошлом периоде ({previousStart:dd.MM} - {previousEnd:dd.MM}) нет данных. Сравнение может быть некорректным.";
            }

            // 3. Агрегируем оба периода
            var current = result.HasCurrentData ? AggregateReports(currentReports) : new BaseReport();
            var previous = result.HasPreviousData ? AggregateReports(previousReports) : new BaseReport();

            result.CurrentPeriod = current;
            result.PreviousPeriod = previous;

            // 4. Вычисляем KPI-дельты
            result.Deltas = new KpiDeltas
            {
                RevenueChange = current.TotalRevenue - previous.TotalRevenue,
                RevenueChangePercent = CalcPercent(current.TotalRevenue, previous.TotalRevenue),

                ProfitChange = current.NetProfit - previous.NetProfit,
                ProfitChangePercent = CalcPercent(current.NetProfit, previous.NetProfit),

                CarsChange = current.TotalCars - previous.TotalCars,
                CarsChangePercent = CalcPercent(current.TotalCars, previous.TotalCars),

                AvgCheckChange = current.AverageCheck - previous.AverageCheck,
                AvgCheckChangePercent = CalcPercent(current.AverageCheck, previous.AverageCheck),

                ExpensesChange = current.TotalExpenses - previous.TotalExpenses,
                ExpensesChangePercent = CalcPercent(current.TotalExpenses, previous.TotalExpenses),

                NewClientsChange = current.NewClientsCount - previous.NewClientsCount,
                NewClientsChangePercent = CalcPercent(current.NewClientsCount, previous.NewClientsCount)
            };

            // 5. Строим данные по дням для графиков
            result.DailyData = BuildDailyComparisonData(currentReports, previousReports, zone);

            // 6. Загруженность по часам (уже есть в BaseReport)
            result.CurrentHourlyLoad = current.HourlyLoad;
            result.PreviousHourlyLoad = previous.HourlyLoad;

            // 7. Сравниваем топ-услуги
            result.ServicesComparison = BuildServicesComparison(current, previous);

            // 8. Сравниваем расходы по категориям
            result.ExpensesComparison = BuildExpensesComparison(current, previous);

            // 9. Сравниваем клиентскую статистику
            result.ClientsComparison = await BuildClientsComparisonAsync(
                branchId, currentStart, currentEnd, previousStart, previousEnd);

            // 10. Сравниваем департаменты
            result.DepartmentsComparison = new DepartmentComparisonData
            {
                Wash = new DepartmentComparison
                {
                    CurrentRevenue = current.WashTotalRevenue,
                    PreviousRevenue = previous.WashTotalRevenue,
                    RevenueChangePercent = CalcPercent(current.WashTotalRevenue, previous.WashTotalRevenue),
                    CurrentCars = current.WashTotalCars,
                    PreviousCars = previous.WashTotalCars,
                    CarsChangePercent = CalcPercent(current.WashTotalCars, previous.WashTotalCars),
                    CurrentProfit = current.WashNetProfit,
                    PreviousProfit = previous.WashNetProfit,
                    ProfitChangePercent = CalcPercent(current.WashNetProfit, previous.WashNetProfit)
                },
                Service = new DepartmentComparison
                {
                    CurrentRevenue = current.ServiceTotalRevenue,
                    PreviousRevenue = previous.ServiceTotalRevenue,
                    RevenueChangePercent = CalcPercent(current.ServiceTotalRevenue, previous.ServiceTotalRevenue),
                    CurrentCars = current.ServiceTotalCars,
                    PreviousCars = previous.ServiceTotalCars,
                    CarsChangePercent = CalcPercent(current.ServiceTotalCars, previous.ServiceTotalCars),
                    CurrentProfit = current.ServiceNetProfit,
                    PreviousProfit = previous.ServiceNetProfit,
                    ProfitChangePercent = CalcPercent(current.ServiceNetProfit, previous.ServiceNetProfit)
                }
            };

            // 11. Сравниваем ФОТ сотрудников
            result.FotComparison = BuildFotComparison(current, previous);

            return Ok(result);
        }

        // ═══════════════════════════════════════════════════════
        //  ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ДЛЯ СРАВНЕНИЯ
        // ═══════════════════════════════════════════════════════

        private static decimal CalcPercent(decimal current, decimal previous)
        {
            if (previous == 0) return current > 0 ? 100m : 0;
            return Math.Round((current - previous) / previous * 100, 1);
        }

        private static decimal CalcPercent(int current, int previous)
        {
            if (previous == 0) return current > 0 ? 100m : 0;
            return Math.Round((decimal)(current - previous) / previous * 100, 1);
        }

        private DailyComparisonData BuildDailyComparisonData(
                List<ShiftReport> currentReports,
                List<ShiftReport> previousReports,
                DateTimeZone zone)
        {
            var result = new DailyComparisonData();

            var currentByDate = currentReports
                .GroupBy(r => r.Date.BusinessDay(zone))
                .Select((g, idx) => new DayComparisonPoint
                {
                    Date = g.Key.ToWireDate(zone),
                    DayIndex = idx,
                    DateLabel = g.Key.ToString("dd.MM", System.Globalization.CultureInfo.InvariantCulture),
                    Revenue = g.Sum(r => r.TotalRevenue),
                    CarsCount = g.Sum(r => r.TotalCars),
                    AvgCheck = g.Sum(r => r.TotalCars) > 0
                        ? Math.Round(g.Sum(r => r.TotalRevenue) / g.Sum(r => r.TotalCars), 0)
                        : 0
                })
                .OrderBy(d => d.Date)
                .ToList();
            for (int i = 0; i < currentByDate.Count; i++) currentByDate[i].DayIndex = i;
            result.CurrentDays = currentByDate;

            var previousByDate = previousReports
                .GroupBy(r => r.Date.BusinessDay(zone))
                .Select((g, idx) => new DayComparisonPoint
                {
                    Date = g.Key.ToWireDate(zone),
                    DayIndex = idx,
                    DateLabel = g.Key.ToString("dd.MM", System.Globalization.CultureInfo.InvariantCulture),
                    Revenue = g.Sum(r => r.TotalRevenue),
                    CarsCount = g.Sum(r => r.TotalCars),
                    AvgCheck = g.Sum(r => r.TotalCars) > 0
                        ? Math.Round(g.Sum(r => r.TotalRevenue) / g.Sum(r => r.TotalCars), 0)
                        : 0
                })
                .OrderBy(d => d.Date)
                .ToList();
            for (int i = 0; i < previousByDate.Count; i++) previousByDate[i].DayIndex = i;
            result.PreviousDays = previousByDate;

            return result;
        }

        private List<ServiceComparisonItem> BuildServicesComparison(BaseReport current, BaseReport previous)
        {
            // Объединяем все уникальные услуги из обоих периодов
            var allServiceNames = current.TopServices
                .Select(s => s.ServiceName)
                .Union(previous.TopServices.Select(s => s.ServiceName))
                .Distinct()
                .ToList();

            var comparison = new List<ServiceComparisonItem>();

            foreach (var serviceName in allServiceNames)
            {
                var curSvc = current.TopServices.FirstOrDefault(s => s.ServiceName == serviceName);
                var prevSvc = previous.TopServices.FirstOrDefault(s => s.ServiceName == serviceName);

                comparison.Add(new ServiceComparisonItem
                {
                    ServiceName = serviceName,
                    CurrentCount = curSvc?.Count ?? 0,
                    CurrentRevenue = curSvc?.TotalRevenue ?? 0,
                    PreviousCount = prevSvc?.Count ?? 0,
                    PreviousRevenue = prevSvc?.TotalRevenue ?? 0,
                    CountChangePercent = CalcPercent(curSvc?.Count ?? 0, prevSvc?.Count ?? 0),
                    RevenueChangePercent = CalcPercent(curSvc?.TotalRevenue ?? 0, prevSvc?.TotalRevenue ?? 0)
                });
            }

            // Сортируем по текущей выручке (топ-5)
            return comparison
                .OrderByDescending(s => s.CurrentRevenue)
                .ThenByDescending(s => s.PreviousRevenue)
                .Take(5)
                .ToList();
        }

        private List<ExpenseComparisonItem> BuildExpensesComparison(BaseReport current, BaseReport previous)
        {
            // Объединяем все уникальные категории из обоих периодов
            var allCategories = current.ExpensesByCategory
                .Select(e => e.Category)
                .Union(previous.ExpensesByCategory.Select(e => e.Category))
                .Distinct()
                .ToList();

            var comparison = new List<ExpenseComparisonItem>();

            foreach (var category in allCategories)
            {
                var curExp = current.ExpensesByCategory.FirstOrDefault(e => e.Category == category);
                var prevExp = previous.ExpensesByCategory.FirstOrDefault(e => e.Category == category);

                comparison.Add(new ExpenseComparisonItem
                {
                    Category = category,
                    CurrentAmount = curExp?.TotalAmount ?? 0,
                    CurrentCount = curExp?.Count ?? 0,
                    PreviousAmount = prevExp?.TotalAmount ?? 0,
                    PreviousCount = prevExp?.Count ?? 0,
                    AmountChangePercent = CalcPercent(curExp?.TotalAmount ?? 0, prevExp?.TotalAmount ?? 0)
                });
            }

            // Сортируем по текущей сумме
            return comparison
                .OrderByDescending(e => e.CurrentAmount)
                .ThenByDescending(e => e.PreviousAmount)
                .ToList();
        }

        private async Task<ClientComparisonData> BuildClientsComparisonAsync(
            int branchId,
            DateTime currentStart, DateTime currentEnd,
            DateTime previousStart, DateTime previousEnd)
        {
            var currentStats = await GetClientsStatsInternal(branchId, currentStart, currentEnd);
            var previousStats = await GetClientsStatsInternal(branchId, previousStart, previousEnd);

            return new ClientComparisonData
            {
                CurrentUnique = currentStats.UniqueClients,
                PreviousUnique = previousStats.UniqueClients,
                CurrentNew = currentStats.NewClients,
                PreviousNew = previousStats.NewClients,
                CurrentRepeat = currentStats.RepeatClients,
                PreviousRepeat = previousStats.RepeatClients,
                CurrentRetentionRate = currentStats.RetentionRate,
                PreviousRetentionRate = previousStats.RetentionRate
            };
        }

        private async Task<ClientStatsResult> GetClientsStatsInternal(int branchId, DateTime start, DateTime end)
        {
            var result = await GetClientsStats(branchId, start, end);
            if (result.Result is OkObjectResult okResult && okResult.Value != null)
            {
                var value = okResult.Value;
                var type = value.GetType();
                return new ClientStatsResult
                {
                    NewClients = (int)(type.GetProperty("NewClients")?.GetValue(value) ?? 0),
                    UniqueClients = (int)(type.GetProperty("UniqueClients")?.GetValue(value) ?? 0),
                    RepeatClients = (int)(type.GetProperty("RepeatClients")?.GetValue(value) ?? 0),
                    RetentionRate = (decimal)(type.GetProperty("RetentionRate")?.GetValue(value) ?? 0m)
                };
            }
            return new ClientStatsResult();
        }

        private class ClientStatsResult
        {
            public int NewClients { get; set; }
            public int UniqueClients { get; set; }
            public int RepeatClients { get; set; }
            public decimal RetentionRate { get; set; }
        }

        private FotComparisonData BuildFotComparison(BaseReport current, BaseReport previous)
        {
            // Объединяем всех сотрудников из обоих периодов
            var allEmployeeIds = current.EmployeesWork
                .Select(e => e.EmployeeId)
                .Union(previous.EmployeesWork.Select(e => e.EmployeeId))
                .Distinct()
                .ToList();

            var employees = new List<EmployeeComparisonItem>();

            foreach (var empId in allEmployeeIds)
            {
                var curEmp = current.EmployeesWork.FirstOrDefault(e => e.EmployeeId == empId);
                var prevEmp = previous.EmployeesWork.FirstOrDefault(e => e.EmployeeId == empId);

                employees.Add(new EmployeeComparisonItem
                {
                    EmployeeId = empId,
                    EmployeeName = curEmp?.EmployeeName ?? prevEmp?.EmployeeName ?? $"Сотрудник #{empId}",
                    CurrentEarnings = curEmp?.Earnings ?? 0,
                    PreviousEarnings = prevEmp?.Earnings ?? 0,
                    EarningsChangePercent = CalcPercent(curEmp?.Earnings ?? 0, prevEmp?.Earnings ?? 0),
                    CurrentCars = curEmp?.CarsWashed ?? 0,
                    PreviousCars = prevEmp?.CarsWashed ?? 0,
                    CurrentAdvances = curEmp?.Advances ?? 0,
                    PreviousAdvances = prevEmp?.Advances ?? 0
                });
            }

            // Сортируем по текущим начислениям
            employees = employees
                .OrderByDescending(e => e.CurrentEarnings)
                .ThenByDescending(e => e.PreviousEarnings)
                .ToList();

            var currentTotalFot = current.TotalWasherEarnings;
            var previousTotalFot = previous.TotalWasherEarnings;
            var currentEmpCount = current.EmployeesWork.Count;
            var previousEmpCount = previous.EmployeesWork.Count;

            return new FotComparisonData
            {
                CurrentTotalFot = currentTotalFot,
                PreviousTotalFot = previousTotalFot,
                TotalFotChangePercent = CalcPercent(currentTotalFot, previousTotalFot),
                CurrentAvgPerEmployee = currentEmpCount > 0 ? Math.Round(currentTotalFot / currentEmpCount, 0) : 0,
                PreviousAvgPerEmployee = previousEmpCount > 0 ? Math.Round(previousTotalFot / previousEmpCount, 0) : 0,
                CurrentEmployeesCount = currentEmpCount,
                PreviousEmployeesCount = previousEmpCount,
                Employees = employees
            };
        }
    }
}