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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Shift>>> GetShifts()
        {
            return await _context.Shifts.ToListAsync();
        }

        // Открыть смену
        [HttpPost]
        public async Task<ActionResult<Shift>> OpenShift(Shift shift)
        {
            // СТАЛО:
            // Убираем эту строку — Branch теперь есть в модели, но при создании смены
            // клиент не должен передавать вложенный объект Branch, только BranchId

            // Приводим дату к UTC без времени для точного поиска
            DateTime targetDate = DateTime.SpecifyKind(shift.Date.Date, DateTimeKind.Utc);

            // Ищем уже существующую смену на этот день и филиал
            Shift existingShift = await _context.Shifts
                .FirstOrDefaultAsync(s => s.BranchId == shift.BranchId && s.Date == targetDate);

            if (existingShift != null)
            {
                // Смена уже была — ВОЗОБНОВЛЯЕМ ЕЁ
                existingShift.IsClosed = false;

                // Обновляем список сотрудников на случай, если вышли другие люди
                existingShift.EmployeeIds = shift.EmployeeIds;

                // Обязательно затираем время закрытия, так как смена снова в работе
                existingShift.EndTime = null;

                _context.Shifts.Update(existingShift);
                await _context.SaveChangesAsync();

                return Ok(existingShift);
            }
            else
            {
                // Смены сегодня еще не было — СОЗДАЕМ НОВУЮ
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

            // 1. ПРОВЕРКА: блокируем, если есть активные заказы в мойке
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

            // 2. Ищем следующую открытую смену в этом филиале
            var nextShift = await _context.Shifts
                .FirstOrDefaultAsync(s =>
                    s.BranchId == shift.BranchId &&
                    s.Id != id &&
                    !s.IsClosed);

            // 3. Находим сервисные заказы "В работе", привязанные к закрываемой смене
            var serviceOrdersToTransfer = await _context.Orders
                .Where(o =>
                    o.ShiftId == id &&
                    o.Department == "Service" &&
                    o.Status == "В работе")
                .ToListAsync();

            var transferredCount = 0;

            if (nextShift != null && serviceOrdersToTransfer.Any())
            {
                // Переносим заказы в новую смену
                foreach (var order in serviceOrdersToTransfer)
                {
                    order.ShiftId = nextShift.Id;

                    // Добавляем запись в ленту о переносе
                    _context.OrderTimelineEntries.Add(new OrderTimelineEntry
                    {
                        OrderId = order.Id,
                        EntryType = TimelineEntryType.ShiftTransferred,
                        Message = $"Заказ автоматически перенесён в смену от {nextShift.Date:dd.MM.yyyy} в связи с закрытием предыдущей смены",
                        CreatedBy = "Система",
                        Timestamp = DateTime.UtcNow,
                        RelatedEntityId = nextShift.Id
                    });

                    transferredCount++;
                }

                await _context.SaveChangesAsync();
            }

            // 4. Закрываем текущую смену
            shift.IsClosed = true;
            shift.EndTime = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // 5. SignalR: уведомляем клиентов
            await _hubContext.Clients.Group($"branch_{shift.BranchId}")
                .SendAsync("UpdateData");

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
            var shift = await _context.Shifts.FindAsync(id);
            if (shift == null) return NotFound();

            // 1. Грузим всё необходимое из базы
            var orders = await _context.Orders
                .Include(o => o.OrderWashers)
                .Where(o => o.ShiftId == id && o.Status == "Выполнен" && o.PaymentMethod == "Наличные")
                .ToListAsync();

            var transactions = await _context.Transactions.Where(t => t.ShiftId == id).ToListAsync();
            var allServices = await _context.Services.ToListAsync();
            var allUsers = await _context.Users.ToListAsync();

            // ДОБАВЛЕНО: Достаем настройки компании для этой смены
            var branch = await _context.Branches.FindAsync(shift.BranchId);
            var settings = await _context.CompanySettings.FindAsync(branch?.CompanyId ?? 0);

            // 2. Считаем выручку
            decimal cashRevenue = orders.Sum(o => o.FinalPrice);

            // 3. Считаем движения по кассе
            decimal deposits = transactions.Where(t => t.Type == "Приход" || t.Type == "Размен").Sum(t => t.Amount);
            decimal advances = transactions.Where(t => t.Type == "Аванс мойщику").Sum(t => t.Amount);
            decimal expenses = transactions.Where(t => t.Type == "Расход").Sum(t => t.Amount);
            decimal withdrawals = transactions.Where(t => t.Type == "Инкассация").Sum(t => t.Amount);

            // 4. Считаем ЗП
            decimal totalTopUp = 0;
            var orderWasherPairs = orders
                .Where(o => o.OrderWashers != null)
                .SelectMany(o => o.OrderWashers,
                        (o, ow) => new { Order = o, OrderWasher = ow, WasherId = ow.UserId })
                .ToList();

            foreach (var group in orderWasherPairs.GroupBy(x => x.WasherId))
            {
                // ИСПРАВЛЕНО: Передаем тип смены (shift.Type) и настройки (settings)
                decimal basePay = group.Sum(x =>
                    Accurat.WebAPI.Services.SalaryCalculationService.CalculateWasherIncomeForOrder(x.OrderWasher, x.Order, allServices, allUsers, shift.Type, settings));

                totalTopUp += basePay;
            }

            // 5. Итоговые цифры
            return new CashboxSummary
            {
                CashInHand = cashRevenue + deposits - (advances + expenses + withdrawals),
                TotalExpenses = expenses + advances,
                NetCashProfit = (cashRevenue * (settings?.CompanySharePercentage ?? 65m) / 100m) - expenses - totalTopUp
            };
        }

    }
}