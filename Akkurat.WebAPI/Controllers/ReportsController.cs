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

            // 1. МУЛЬТИФИЛИАЛЬНОСТЬ: Если branchId == 0, запрос не фильтрует по филиалу (берем всю сеть)
            var shiftsQuery = _context.Shifts
                .Where(s => s.IsClosed && s.Date >= startUtc && s.Date <= endUtc);

            if (branchId > 0)
            {
                shiftsQuery = shiftsQuery.Where(s => s.BranchId == branchId);
            }

            var shifts = await shiftsQuery.ToListAsync();
            var reports = new List<ShiftReport>();
            var allUsers = await _context.Users.ToListAsync();

            // 🔥 ИСПРАВЛЕНО: Загружаем все услуги из базы данных один раз перед циклами
            var allServices = await _context.Services.ToListAsync();

            foreach (var shift in shifts)
            {
                var orders = await _context.Orders
                    .Include(o => o.OrderWashers)
                        .ThenInclude(ow => ow.Washer) // Загружаем мойщиков
                    .Where(o => o.ShiftId == shift.Id && o.Status == "Выполнен").ToListAsync();
                var transactions = await _context.Transactions.Where(t => t.ShiftId == shift.Id).ToListAsync();

                // Разделяем заказы по департаментам
                var washOrders = orders.Where(o => o.Department == "Wash").ToList();
                var serviceOrders = orders.Where(o => o.Department == "Service").ToList();

                var report = new ShiftReport
                {
                    Id = shift.Id,
                    Date = shift.Date,
                    StartTime = shift.StartTime ?? shift.Date,
                    EndTime = shift.EndTime,
                    Notes = shift.Notes,

                    // Общие данные
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

                    // 2. ДЕПАРТАМЕНТИЗАЦИЯ: Мойка
                    WashTotalCars = washOrders.Count,
                    WashTotalRevenue = washOrders.Sum(o => o.FinalPrice),
                    WashTotalExpenses = transactions.Where(t => t.Type == "Расход" && t.Department == "Wash").Sum(t => t.Amount),

                    // 2. ДЕПАРТАМЕНТИЗАЦИЯ: Сервис
                    ServiceTotalCars = serviceOrders.Count,
                    ServiceTotalRevenue = serviceOrders.Sum(o => o.FinalPrice),
                    ServiceTotalExpenses = transactions.Where(t => t.Type == "Расход" && t.Department == "Service").Sum(t => t.Amount)
                };

                decimal totalWasherEarnings = 0;
                decimal totalCompanyEarnings = 0;

                decimal totalWashCompanyEarnings = 0;
                decimal totalServiceCompanyEarnings = 0;

                // РАЗВОРАЧИВАЕМ ЗАКАЗЫ ПО МОЙЩИКАМ (SOFT SPLIT)
                var orderWasherPairs = orders
                    .SelectMany(o => o.OrderWashers ?? new List<OrderWasher>(),
                               (o, ow) => new { Order = o, OrderWasher = ow, WasherId = ow.UserId, SplitShare = ow.SplitShare })
                    .ToList();

                foreach (var group in orderWasherPairs.GroupBy(x => x.WasherId))
                {
                    if (group.Key == 0) continue;

                    var emp = allUsers.FirstOrDefault(u => u.Id == group.Key);
                    decimal empRevenue = group.Sum(x => x.Order.FinalPrice * x.SplitShare);

                    // ВЫЗЫВАЕМ ЯДРО РАСЧЕТА
                    decimal empBaseEarnings = group.Sum(x =>
                        Accurat.WebAPI.Services.SalaryCalculationService.CalculateWasherIncomeForOrder(x.OrderWasher, x.Order, allServices));

                    decimal empTotalEarnings = empBaseEarnings;
                    decimal empAdvances = transactions.Where(t => t.EmployeeId == emp?.Id && t.Type == "Аванс мойщику").Sum(t => t.Amount);

                    totalWasherEarnings += empTotalEarnings;
                    totalCompanyEarnings += (empRevenue - empTotalEarnings);

                    // Разделение доли компании
                    var washItems = group.Where(x => x.Order.Department == "Wash");
                    var serviceItems = group.Where(x => x.Order.Department == "Service");

                    var empWashRevenue = washItems.Sum(x => x.Order.FinalPrice * x.SplitShare);
                    var empServiceRevenue = serviceItems.Sum(x => x.Order.FinalPrice * x.SplitShare);

                    var empWashBase = washItems.Sum(x => Accurat.WebAPI.Services.SalaryCalculationService.CalculateWasherIncomeForOrder(x.OrderWasher, x.Order, allServices));
                    var empServiceBase = serviceItems.Sum(x => Accurat.WebAPI.Services.SalaryCalculationService.CalculateWasherIncomeForOrder(x.OrderWasher, x.Order, allServices));

                    totalWashCompanyEarnings += (empWashRevenue - empWashBase);
                    totalServiceCompanyEarnings += (empServiceRevenue - empServiceBase);

                    report.EmployeesWork.Add(new EmployeeReport
                    {
                        EmployeeId = emp?.Id ?? 0,
                        EmployeeName = emp?.FullName ?? "Неизвестно",
                        CarsWashed = group.Count(), // Считаем количество участий в заказах
                        TotalAmount = empRevenue,
                        Earnings = empTotalEarnings,
                        Advances = empAdvances
                    });
                }

                report.TotalWasherEarnings = totalWasherEarnings;
                report.TotalCompanyEarnings = totalCompanyEarnings;
                report.WashCompanyEarnings = totalWashCompanyEarnings;
                report.ServiceCompanyEarnings = totalServiceCompanyEarnings;

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

            // Если передан конкретный филиал, фильтруем. Иначе — считаем по всей сети.
            if (branchId > 0)
            {
                // Заметка: У клиента может не быть привязки к филиалу, но уникальных считаем по заказам филиала
                uniqueClientsQuery = uniqueClientsQuery.Where(o => o.BranchId == branchId);
            }

            int newClients = await newClientsQuery.CountAsync();
            int uniqueClients = await uniqueClientsQuery.Select(o => o.ClientId).Distinct().CountAsync();

            return Ok(new { NewClients = newClients, UniqueClients = uniqueClients });
        }
    }
}