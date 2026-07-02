using Accurat.WebAPI.Data;
using Accurat.WebAPI.Hubs;
using AccuratSystem.Contracts.DTOs;
using AccuratSystem.Contracts.Enums;
using AccuratSystem.Contracts.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Accurat.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShiftsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<AppHub> _hubContext;

        public ShiftsController(AppDbContext context, IHubContext<AppHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // === ДОБАВЛЕНО: Чтение ID компании из заголовка (SaaS защита) ===
        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id)
            ? int.Parse(id)
            : 1;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Shift>>> GetShifts()
        {
            return await _context.Shifts.ToListAsync();
        }

        // Открыть смену
        [HttpPost]
        public async Task<ActionResult<Shift>> OpenShift(Shift shift)
        {
            DateTime targetDate = DateTime.SpecifyKind(shift.Date.Date, DateTimeKind.Utc);

            Shift existingShift = await _context.Shifts
                .FirstOrDefaultAsync(s => s.BranchId == shift.BranchId && s.Date == targetDate);

            if (existingShift != null)
            {
                existingShift.IsClosed = false;
                existingShift.EmployeeIds = shift.EmployeeIds;
                existingShift.EndTime = null;

                _context.Shifts.Update(existingShift);
                await _context.SaveChangesAsync();

                return Ok(existingShift);
            }
            else
            {
                shift.StartTime = DateTime.UtcNow;
                shift.Date = targetDate;
                shift.IsClosed = false;

                _context.Shifts.Add(shift);
                await _context.SaveChangesAsync();

                return Ok(shift);
            }
        }

        // Закрыть смену
        [HttpPatch("{id}/close")]
        public async Task<IActionResult> CloseShift(int id)
        {
            var shift = await _context.Shifts.FindAsync(id);
            if (shift == null) return NotFound("Смена не найдена");
            if (shift.IsClosed) return BadRequest("Смена уже закрыта");

            var activeWashOrders = await _context.Orders
                .Where(o => o.ShiftId == id && o.Department == "Wash" && o.Status == "В работе")
                .Select(o => new { o.Id, o.CarNumber })
                .ToListAsync();

            if (activeWashOrders.Any())
            {
                return BadRequest(new
                {
                    message = "Нельзя закрыть смену: есть активные заказы в мойке",
                    orders = activeWashOrders
                });
            }

            var branch = await _context.Branches.FindAsync(shift.BranchId);
            var settings = await _context.CompanySettings.FindAsync(branch?.CompanyId ?? 0);

            var completedOrders = await _context.Orders
                .Where(o => o.ShiftId == id && (o.Status == "Выполнен" || o.Status == "Завершен"))
                .ToListAsync();

            var allUsers = await _context.Users.ToListAsync();
            var allServices = await _context.Services.ToListAsync();

            decimal totalAdminPayForShift = 0;

            var adminsInShift = shift.EmployeeIds?.Where(uid => {
                var u = allUsers.FirstOrDefault(x => x.Id == uid);
                return u != null && (u.RoleId == 1 || u.RoleId == 2);
            }).ToList() ?? new List<int>();

            foreach (var adminId in adminsInShift)
            {
                var admin = allUsers.First(u => u.Id == adminId);
                var stats = OrderMath.CalculateShiftStats(completedOrders, allServices, admin, shift.Type, allUsers, settings);
                totalAdminPayForShift += stats.TotalEarned;
            }

            shift.AdminEarningsSnapshot = totalAdminPayForShift;

            var nextShift = await _context.Shifts
                .FirstOrDefaultAsync(s =>
                    s.BranchId == shift.BranchId &&
                    s.Id != id &&
                    !s.IsClosed);

            var serviceOrdersToTransfer = await _context.Orders
                .Where(o => o.ShiftId == id && o.Department == "Service" && o.Status == "В работе")
                .ToListAsync();

            var transferredCount = 0;
            if (nextShift != null && serviceOrdersToTransfer.Any())
            {
                foreach (var order in serviceOrdersToTransfer)
                {
                    order.ShiftId = nextShift.Id;
                    _context.OrderTimelineEntries.Add(new OrderTimelineEntry
                    {
                        OrderId = order.Id,
                        EntryType = TimelineEntryType.ShiftTransferred,
                        Message = $"Заказ автоматически перенесён в смену от {nextShift.Date:dd.MM.yyyy}",
                        CreatedBy = "Система",
                        Timestamp = DateTime.UtcNow,
                        RelatedEntityId = nextShift.Id
                    });
                    transferredCount++;
                }
                await _context.SaveChangesAsync();
            }

            shift.IsClosed = true;
            shift.EndTime = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("UpdateData");

            return Ok(new
            {
                shift.Id,
                shift.EndTime,
                transferredCount,
                nextShiftId = nextShift?.Id
            });
        }

        [HttpGet("{id}/cashbox")]
        public async Task<ActionResult<CashboxSummary>> GetCashboxSummary(int id)
        {
            // ИСПРАВЛЕНО: Используем Include(s => s.Branch), чтобы Branch не был null
            var shift = await _context.Shifts
                .Include(s => s.Branch)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (shift == null) return NotFound();

            // Теперь CurrentCompanyId определен в начале класса, и ошибка исчезнет
            if (CurrentCompanyId != 0 && shift.Branch != null && shift.Branch.CompanyId != CurrentCompanyId)
            {
                return Forbid("Вы не имеете доступа к кассе другого филиала!");
            }

            var orders = await _context.Orders
                .Include(o => o.OrderWashers)
                .Where(o => o.ShiftId == id && o.Status == "Выполнен" && o.PaymentMethod == "Наличные")
                .ToListAsync();

            var transactions = await _context.Transactions.Where(t => t.ShiftId == id).ToListAsync();
            var allServices = await _context.Services.ToListAsync();
            var allUsers = await _context.Users.ToListAsync();

            var branch = await _context.Branches.FindAsync(shift.BranchId);
            var settings = await _context.CompanySettings.FindAsync(branch?.CompanyId ?? 0);

            decimal cashRevenue = orders.Sum(o => o.FinalPrice);
            decimal deposits = transactions.Where(t => t.Type == "Приход" || t.Type == "Размен").Sum(t => t.Amount);
            decimal advances = transactions.Where(t => t.Type == "Аванс мойщику").Sum(t => t.Amount);
            decimal expenses = transactions.Where(t => t.Type == "Расход").Sum(t => t.Amount);
            decimal withdrawals = transactions.Where(t => t.Type == "Инкассация").Sum(t => t.Amount);

            decimal totalTopUp = 0;
            var orderWasherPairs = orders
                .Where(o => o.OrderWashers != null)
                .SelectMany(o => o.OrderWashers,
                        (o, ow) => new { Order = o, OrderWasher = ow, WasherId = ow.UserId })
                .ToList();

            foreach (var group in orderWasherPairs.GroupBy(x => x.WasherId))
            {
                decimal basePay = group.Sum(x =>
                    Accurat.WebAPI.Services.SalaryCalculationService.CalculateWasherIncomeForOrder(x.OrderWasher, x.Order, allServices, allUsers, shift.Type, settings));

                totalTopUp += basePay;
            }

            return new CashboxSummary
            {
                CashInHand = cashRevenue + deposits - (advances + expenses + withdrawals),
                TotalExpenses = expenses + advances,
                NetCashProfit = (cashRevenue * (settings?.CompanySharePercentage ?? 65m) / 100m) - expenses - totalTopUp
            };
        }
    }
}
