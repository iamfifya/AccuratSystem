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

        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id)
            ? int.Parse(id)
            : 1;

        #region Хелперы безопасности

        // Проверяет, принадлежит ли смена текущей компании
        private async Task<Shift?> VerifyShiftAccess(int shiftId)
        {
            if (CurrentCompanyId == 0) return await _context.Shifts.FindAsync(shiftId);

            var shift = await _context.Shifts
                .Include(s => s.Branch)
                .FirstOrDefaultAsync(s => s.Id == shiftId);

            if (shift == null || shift.Branch == null || shift.Branch.CompanyId != CurrentCompanyId)
                return null;

            return shift;
        }

        // Проверяет, принадлежит ли филиал текущей компании
        private async Task<bool> VerifyBranchAccess(int branchId)
        {
            if (CurrentCompanyId == 0) return true;
            var branch = await _context.Branches.FindAsync(branchId);
            return branch != null && branch.CompanyId == CurrentCompanyId;
        }
        #endregion

        #region X-ОТЧЕТ (СВЕРКА КАССЫ)

        /// <summary>
        /// Общий метод расчёта кассы. Используется и в GET cashbox (отображение), 
        /// и в POST reconcile (сохранение X-отчета). Обе точки считают по ОДНОЙ формуле.
        /// </summary>
        private async Task<CashboxSummary> GetCashboxSummaryInternalAsync(int shiftId)
        {
            var shift = await _context.Shifts
                .Include(s => s.Branch)
                .FirstOrDefaultAsync(s => s.Id == shiftId);

            if (shift == null)
                throw new InvalidOperationException($"Смена #{shiftId} не найдена");

            var orders = await _context.Orders
                .Include(o => o.OrderWashers)
                .Where(o => o.ShiftId == shiftId
                         && (o.Status == "Выполнен" || o.Status == "Завершен")
                         && o.PaymentMethod == "Наличные")
                .ToListAsync();

            var transactions = await _context.Transactions.Where(t => t.ShiftId == shiftId).ToListAsync();

            // Наличная выручка
            decimal cashRevenue = orders.Sum(o => o.FinalPrice);

            // Приходы и размен (добавляются в кассу)
            decimal deposits = transactions
                .Where(t => t.Type == "Приход" || t.Type == "Размен")
                .Sum(t => t.Amount);

            // Авансы мойщикам (уходят из кассы)
            decimal advances = transactions
                .Where(t => t.Type == "Аванс мойщику")
                .Sum(t => t.Amount);

            // Расходы (уходят из кассы)
            decimal expenses = transactions
                .Where(t => t.Type == "Расход")
                .Sum(t => t.Amount);

            // Инкассации (уходят из кассы)
            decimal withdrawals = transactions
                .Where(t => t.Type == "Инкассация")
                .Sum(t => t.Amount);

            // ЗП мойщика берём из замороженных EarnedAmount (гарантирует совпадение с отчётами)
            decimal washerPay = orders
                .Where(o => o.OrderWashers != null)
                .SelectMany(o => o.OrderWashers)
                .Sum(ow => ow.EarnedAmount);

            // Чистая прибыль компании = выручка - ЗП мойщика
            decimal companyCashEarnings = cashRevenue - washerPay;

            return new CashboxSummary
            {
                CashInHand = cashRevenue + deposits - (advances + expenses + withdrawals),
                TotalExpenses = expenses + advances,
                NetCashProfit = companyCashEarnings - expenses
            };
        }

        /// <summary>
        /// Общий метод расчёта ожидаемой наличности в кассе.
        /// </summary>
        private async Task<decimal> ComputeCashInHandAsync(int shiftId)
        {
            var summary = await GetCashboxSummaryInternalAsync(shiftId);
            return summary.CashInHand;
        }

        /// <summary>
        /// POST /Shifts/{id}/reconcile — сохранить пересчёт кассы (X-отчет).
        /// </summary>
        [HttpPost("{id}/reconcile")]
        public async Task<IActionResult> ReconcileCash(int id, [FromBody] ReconcileCashRequest request)
        {
            if (request == null) return BadRequest("Тело запроса пустое");

            var shift = await _context.Shifts
                .Include(s => s.Branch)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (shift == null) return NotFound($"Смена #{id} не найдена");

            if (CurrentCompanyId != 0 && shift.Branch?.CompanyId != CurrentCompanyId)
                return Forbid();

            var expected = await ComputeCashInHandAsync(id);

            var record = new CashReconciliation
            {
                ShiftId = id,
                ExpectedCash = expected,
                ActualCash = request.ActualCash,
                Difference = request.ActualCash - expected,
                CountedById = request.CountedById,
                CountedBy = request.CountedBy ?? "Неизвестно",
                Comment = request.Comment ?? "",
                CountedAt = DateTime.UtcNow
            };

            _context.CashReconciliations.Add(record);

            // Логируем событие пересчёта кассы в Журнал действий смены ShiftTimelineEntries
            _context.ShiftTimelineEntries.Add(new ShiftTimelineEntry
            {
                ShiftId = id,
                EventType = "CashReconciliation",
                Message = record.Difference == 0
                    ? $"Пересчёт кассы: ожидалось {expected:N0} ₽, факт {request.ActualCash:N0} ₽ — касса сошлась"
                    : $"Пересчёт кассы: ожидалось {expected:N0} ₽, факт {request.ActualCash:N0} ₽ — {(record.Difference < 0 ? "недостача" : "излишек")} {Math.Abs(record.Difference):N0} ₽",
                CreatedBy = record.CountedBy,
                Timestamp = DateTime.UtcNow,
                RelatedEntityId = record.Id
            });

            await _context.SaveChangesAsync();

            return Ok(new ReconcileCashResult
            {
                Id = record.Id,
                ExpectedCash = record.ExpectedCash,
                ActualCash = record.ActualCash,
                Difference = record.Difference
            });
        }

        [HttpGet("{id}/timeline")]
        public async Task<ActionResult<IEnumerable<ShiftTimelineEntry>>> GetShiftTimeline(int id)
        {
            var shift = await VerifyShiftAccess(id);
            if (shift == null) return (CurrentCompanyId != 0) ? Forbid() : NotFound();

            var list = await _context.ShiftTimelineEntries
                .Where(e => e.ShiftId == id)
                .OrderByDescending(e => e.Timestamp)
                .ToListAsync();

            return Ok(list);
        }

        /// <summary>
        /// GET /Shifts/{id}/reconciliations — история пересчётов смены.
        /// </summary>
        [HttpGet("{id}/reconciliations")]
        public async Task<ActionResult<IEnumerable<CashReconciliation>>> GetReconciliations(int id)
        {
            var shift = await _context.Shifts
                .Include(s => s.Branch)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (shift == null) return NotFound($"Смена #{id} не найдена");

            if (CurrentCompanyId != 0 && shift.Branch?.CompanyId != CurrentCompanyId)
                return Forbid();

            var list = await _context.CashReconciliations
                .Where(r => r.ShiftId == id)
                .OrderByDescending(r => r.CountedAt)
                .ToListAsync();

            return Ok(list);
        }

        #endregion

        [HttpGet("{id}/cashbox")]
        public async Task<ActionResult<CashboxSummary>> GetCashboxSummary(int id)
        {
            var shift = await VerifyShiftAccess(id);
            if (shift == null) return (CurrentCompanyId != 0) ? Forbid() : NotFound();

            var summary = await GetCashboxSummaryInternalAsync(id);
            return Ok(summary);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Shift>>> GetShifts()
        {
            var query = _context.Shifts.AsQueryable();

            // ИЗОЛЯЦИЯ: Возвращаем только смены своих филиалов
            if (CurrentCompanyId != 0)
            {
                var myBranchIds = _context.Branches
                    .Where(b => b.CompanyId == CurrentCompanyId)
                    .Select(b => b.Id);
                query = query.Where(s => myBranchIds.Contains(s.BranchId));
            }

            return await query.ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Shift>> OpenShift(Shift shift)
        {
            // БЕЗОПАСНОСТЬ: Проверяем, имеет ли пользователь право открывать смену в этом филиале
            if (!await VerifyBranchAccess(shift.BranchId))
                return Forbid("Вы не имеете прав на открытие смены в данном филиале.");

            DateTime targetDate = DateTime.SpecifyKind(shift.Date.Date, DateTimeKind.Utc);

            Shift existingShift = await _context.Shifts
                .FirstOrDefaultAsync(s => s.BranchId == shift.BranchId && s.Date == targetDate);

            if (existingShift != null)
            {
                // Реактивируем существующую закрытую смену
                existingShift.IsClosed = false;
                existingShift.EmployeeIds = shift.EmployeeIds;
                existingShift.EndTime = null;
                existingShift.StartTime = DateTime.UtcNow;  // Добавил: обновляем время открытия
                existingShift.AdminEarningsSnapshot = 0;     // Добавил: сбрасываем снимок зарплаты

                // ИСПРАВЛЕНО: используем existingShift.Id вместо shift.Id
                // Загружаем название филиала из БД
                var branch = await _context.Branches.FindAsync(shift.BranchId);

                _context.ShiftTimelineEntries.Add(new ShiftTimelineEntry
                {
                    ShiftId = existingShift.Id,  // ✅ Используем ID существующей смены
                    EventType = "ShiftOpened",
                    Message = $"Смена переоткрыта на {branch?.Name ?? "филиале"}",
                    CreatedBy = "Система",
                    Timestamp = DateTime.UtcNow
                });

                // ОДИН SaveChanges для обеих операций — EF Core сам обработает зависимости
                await _context.SaveChangesAsync();

                return Ok(existingShift);
            }
            else
            {
                // Создаём новую смену
                shift.StartTime = DateTime.UtcNow;
                shift.Date = targetDate;
                shift.IsClosed = false;
                shift.AdminEarningsSnapshot = 0;

                _context.Shifts.Add(shift);
                await _context.SaveChangesAsync();  // shift.Id теперь заполнен БД

                // Логируем открытие новой смены
                var branch = await _context.Branches.FindAsync(shift.BranchId);
                _context.ShiftTimelineEntries.Add(new ShiftTimelineEntry
                {
                    ShiftId = shift.Id,  // ✅ ID уже сгенерирован БД
                    EventType = "ShiftOpened",
                    Message = $"Смена открыта на {branch?.Name ?? "филиале"}",
                    CreatedBy = "Система",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                return Ok(shift);
            }
        }

        [HttpPatch("{id}/close")]
        public async Task<IActionResult> CloseShift(int id)
        {
            // БЕЗОПАСНОСТЬ: Проверяем владение сменой
            var shift = await VerifyShiftAccess(id);
            if (shift == null) return (CurrentCompanyId != 0) ? Forbid() : NotFound("Смена не найдена");

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

            // Здесь branch уже подгружен через VerifyShiftAccess
            var settings = await _context.CompanySettings.FindAsync(shift.Branch?.CompanyId ?? 0);

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

            // Логируем событие закрытия смены в Журнал действий смены ShiftTimelineEntries
            _context.ShiftTimelineEntries.Add(new ShiftTimelineEntry
            {
                ShiftId = id,
                EventType = "ShiftClosed",
                Message = $"Смена закрыта. Заказов: {completedOrders.Count}, выручка: {completedOrders.Sum(o => o.FinalPrice):N0} ₽",
                CreatedBy = "Система",
                Timestamp = DateTime.UtcNow,
                RelatedEntityId = completedOrders.Count
            });

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

        
    }
}
