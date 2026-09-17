using Accurat.WebAPI.Data;
using Accurat.WebAPI.Hubs;
using Accurat.WebAPI.Time;
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
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<AppHub> _hubContext;

        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id)
            ? int.Parse(id)
            : 1;

        public OrdersController(AppDbContext context, IHubContext<AppHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        #region Хелперы безопасности (SaaS Isolation)

        // Проверяет, принадлежит ли филиал текущей компании
        private async Task<bool> VerifyBranchAccess(int branchId)
        {
            if (CurrentCompanyId == 0) return true; // Режим разработчика

            var branch = await _context.Branches.FindAsync(branchId);
            return branch != null && branch.CompanyId == CurrentCompanyId;
        }

        // Проверяет, принадлежит ли заказ текущей компании
        private async Task<Order> VerifyOrderAccess(int orderId)
        {
            if (CurrentCompanyId == 0) return await _context.Orders.FindAsync(orderId);

            var order = await _context.Orders
                .Include(o => o.Branch)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null || order.Branch == null || order.Branch.CompanyId != CurrentCompanyId)
                return null;

            return order;
        }
        #endregion

        [HttpPost("calculate-preview")]
        public async Task<ActionResult<OrderCalculation>> CalculatePreview([FromBody] OrderPreviewRequestDto request)
        {
            try
            {
                // БЕЗОПАСНОСТЬ: Проверяем доступ к филиалу
                if (!await VerifyBranchAccess(request.BranchId))
                    return Forbid("Доступ к данным этого филиала запрещен.");

                var branch = await _context.Branches.FindAsync(request.BranchId);
                var settings = await _context.CompanySettings.FindAsync(branch.CompanyId);

                var services = await _context.Services
                    .Where(s => request.ServiceIds.Contains(s.Id))
                    .ToListAsync();

                var washers = new List<User>();
                if (request.WasherId > 0)
                {
                    var washer = await _context.Users.FindAsync(request.WasherId);
                    if (washer != null) washers.Add(washer);
                }

                var virtualOrder = new Order
                {
                    BranchId = request.BranchId,
                    ServiceIds = request.ServiceIds,
                    BodyTypeCategory = request.BodyTypeCategory,
                    ExtraCost = request.ExtraCost,
                    DiscountPercent = request.DiscountPercent,
                    DiscountAmount = request.DiscountAmount,
                    Notes = request.Notes ?? string.Empty
                };

                if (request.WasherId > 0)
                {
                    virtualOrder.OrderWashers = new List<OrderWasher>
                    {
                        new OrderWasher { OrderId = 0, UserId = request.WasherId, SplitShare = 1.0m }
                    };
                }

                var calculation = OrderMath.Calculate(virtualOrder, services, washers, settings, request.ShiftType);
                return Ok(calculation);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка калькулятора на сервере: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrders([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            DateTime start = BusinessTime.ToInstantUtc(startDate ?? DateTime.UtcNow.AddDays(-1));
            DateTime end = BusinessTime.ToInstantUtc(endDate ?? DateTime.UtcNow.AddDays(30));

            var query = _context.Orders.AsQueryable();

            if (CurrentCompanyId != 0)
            {
                var myBranchIds = _context.Branches
                    .Where(b => b.CompanyId == CurrentCompanyId)
                    .Select(b => b.Id);
                query = query.Where(o => myBranchIds.Contains(o.BranchId));
            }

            query = query.Where(o => o.Time >= start && o.Time <= end);
            var orders = await query.Include(o => o.OrderWashers).ToListAsync();

            foreach (var order in orders)
            {
                var latestHistory = await _context.OrderStatusHistories
                    .FirstOrDefaultAsync(h => h.OrderId == order.Id && h.EndTime == null);
                order.CurrentStatusStartTime = latestHistory?.StartTime;
            }

            return orders;
        }

        [HttpPost]
        public async Task<ActionResult<Order>> CreateOrder(Order order)
        {
            if (order.Status == "Выполнен" && (string.IsNullOrWhiteSpace(order.PaymentMethod) || order.PaymentMethod == "Не указано"))
            {
                return BadRequest("Для выполненного заказа требуется указать способ оплаты.");
            }

            // БЕЗОПАСНОСТЬ: Проверяем, что заказ создается в филиале текущей компании
            if (!await VerifyBranchAccess(order.BranchId))
                return Forbid("Вы не можете создавать заказы в этом филиале.");

            using (var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    var washersFromClient = order.OrderWashers?.ToList() ?? new List<OrderWasher>();
                    order.OrderWashers = new List<OrderWasher>();

                    foreach (var ow in washersFromClient)
                    {
                        order.OrderWashers.Add(new OrderWasher
                        {
                            UserId = ow.UserId,
                            SplitShare = ow.SplitShare
                            // OrderId проставится автоматически при SaveChanges
                        });
                    }

                    var endTime = order.Time.AddMinutes(order.DurationMinutes > 0 ? order.DurationMinutes : 60);

                    bool hasConflict = await _context.Orders.AnyAsync(o =>
                        o.BoxNumber == order.BoxNumber &&
                        o.BranchId == order.BranchId &&
                        o.Status != "Отменен" &&
                        o.Status != "Выполнен" &&
                        o.Time < endTime &&
                        o.Time.AddMinutes(o.DurationMinutes > 0 ? o.DurationMinutes : 60) > order.Time);

                    if (hasConflict) return BadRequest(new { message = "Выбранное время в данном боксе уже занято" });

                    if (string.IsNullOrEmpty(order.Status)) order.Status = "В работе";

                    var branch = await _context.Branches.FindAsync(order.BranchId);
                    var settings = await _context.CompanySettings.FindAsync(branch?.CompanyId ?? 0);
                    var services = await _context.Services.Where(s => order.ServiceIds.Contains(s.Id)).ToListAsync();
                    var washers = await _context.Users.Where(u => order.OrderWashers.Select(ow => ow.UserId).Contains(u.Id)).ToListAsync();

                    var finalCalc = OrderMath.Calculate(order, services, washers, settings);
                    order.FinalPrice = finalCalc.FinalPrice;

                    _context.Orders.Add(order);
                    await _context.SaveChangesAsync();

                    // Снапшоты цен (OrderServiceItems)
                    if (order.OrderServiceItems == null) order.OrderServiceItems = new List<OrderServiceItem>();
                    foreach (var serviceId in order.ServiceIds)
                    {
                        var service = await _context.Services.FindAsync(serviceId);
                        if (service != null)
                        {
                            decimal currentPrice = service.PriceByBodyType.TryGetValue(order.BodyTypeCategory, out var p) ? p :
                                                   (service.PriceByBodyType.TryGetValue(1, out var def) ? def : 0);

                            order.OrderServiceItems.Add(new OrderServiceItem
                            {
                                OrderId = order.Id,
                                ServiceId = serviceId,
                                ActualPrice = currentPrice,
                                Quantity = 1
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                    transaction.Commit();
                    await _hubContext.Clients.All.SendAsync("UpdateData");

                    return Ok(order);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return StatusCode(500, "Внутренняя ошибка сервера: " + ex.Message);
                }
            }
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Order>> GetOrderById(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderWashers)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            // Изоляция тенанта: заказ должен принадлежать компании запроса
            if (CurrentCompanyId != 0)
            {
                var branch = await _context.Branches.FindAsync(order.BranchId);
                if (branch == null || branch.CompanyId != CurrentCompanyId) return Forbid();
            }

            return Ok(order);
        }

        [HttpPost("{id}/convert")]
        public async Task<ActionResult<Order>> ConvertToOrder(int id, [FromQuery] int shiftId, [FromQuery] int washerId)
        {
            // БЕЗОПАСНОСТЬ: Проверка владения заказом
            var order = await VerifyOrderAccess(id);
            if (order == null) return (CurrentCompanyId != 0) ? Forbid() : NotFound($"Запись {id} не найдена.");

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Дозагружаем мойщиков, если они пропали при VerifyOrderAccess
                    await _context.Entry(order).Collection(o => o.OrderWashers).LoadAsync();

                    order.IsAppointment = false;
                    order.Status = "В работе";
                    order.ShiftId = shiftId;
                    order.Time = DateTime.UtcNow;

                    order.OrderWashers.Clear();
                    order.OrderWashers.Add(new OrderWasher { OrderId = order.Id, UserId = washerId, SplitShare = 1.0m });

                    _context.OrderStatusHistories.Add(new AccuratSystem.Contracts.Models.OrderStatusHistories
                    {
                        OrderId = order.Id,
                        Status = "В работе",
                        StartTime = DateTime.UtcNow,
                        UserId = washerId
                    });

                    _context.OrderTimelineEntries.Add(new AccuratSystem.Contracts.Models.OrderTimelineEntry
                    {
                        OrderId = order.Id,
                        EntryType = TimelineEntryType.StatusChanged,
                        Message = "Предварительная запись переведена в работу",
                        CreatedBy = "Система (Конвертация)",
                        Timestamp = DateTime.UtcNow
                    });

                    var branch = await _context.Branches.FindAsync(order.BranchId);
                    var settings = await _context.CompanySettings.FindAsync(branch?.CompanyId ?? 0);
                    var services = await _context.Services.Where(s => order.ServiceIds.Contains(s.Id)).ToListAsync();
                    var washer = await _context.Users.FindAsync(washerId);

                    var finalCalc = OrderMath.Calculate(order, services, washer != null ? new List<User> { washer } : null, settings);
                    order.FinalPrice = finalCalc.FinalPrice;

                    _context.Entry(order).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                    transaction.Commit();
                    await _hubContext.Clients.All.SendAsync("UpdateData");

                    return Ok(order);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return StatusCode(500, "Ошибка конвертации: " + ex.Message);
                }
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOrder(int id, Order order)
        {
            if (id != order.Id) return BadRequest("ID не совпадают");

            // 1. Загружаем существующий заказ ВМЕСТЕ с мойщиками (Include)
            var existingOrder = await _context.Orders
                .Include(o => o.OrderWashers)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (existingOrder == null) return NotFound();

            if (existingOrder.Status == "Выполнен" || existingOrder.Status == "Завершен")
            {
                // Если статус не изменился, значит пытаются править цены/услуги — блокируем
                if (existingOrder.Status == order.Status)
                    return BadRequest("Нельзя редактировать цены или услуги в выполненном заказе. Доступна только смена статуса.");

                // Разрешаем ТОЛЬКО смену статуса (например, на "Отменен")
                existingOrder.Status = order.Status;

                // Логируем изменение в ленту
                _context.OrderTimelineEntries.Add(new AccuratSystem.Contracts.Models.OrderTimelineEntry
                {
                    OrderId = id,
                    EntryType = TimelineEntryType.StatusChanged,
                    Message = $"Статус изменен на: {existingOrder.Status} (Заказ ранее был выполнен)",
                    CreatedBy = await GetActorName(),
                    Timestamp = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("UpdateData");
                return NoContent(); // Успешно сохранили только статус, выходим
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var oldStatus = existingOrder.Status;

                // === НОВОЕ: АУДИТ ФИНАНСОВЫХ ИЗМЕНЕНИЙ ===
                // Вызываем ДО SetValues: existingOrder ещё содержит состояние из БД,
                // order — присланное клиентом. Серверное сравнение нельзя подделать.
                await AuditPriceChanges(existingOrder, order);

                // === НОВОЕ: АУДИТ ОПЕРАЦИОННЫХ ИЗМЕНЕНИЙ ===
                await AuditOperationalChanges(existingOrder, order);

                // Сохраняем поля, которые запрещено менять с клиента
                var dbBranchId = existingOrder.BranchId;
                var dbShiftId = existingOrder.ShiftId;
                var dbAdminId = existingOrder.AdminId;
                var dbFinishedAt = existingOrder.FinishedAt;
                var dbGeneralNotes = existingOrder.GeneralNotes;

                // Копируем скалярные свойства из пришедшего order
                _context.Entry(existingOrder).CurrentValues.SetValues(order);

                // Возвращаем защищенные поля
                existingOrder.BranchId = dbBranchId;
                existingOrder.ShiftId = dbShiftId;
                existingOrder.AdminId = dbAdminId;
                existingOrder.FinishedAt = dbFinishedAt;
                existingOrder.GeneralNotes = dbGeneralNotes;

                // 2. Обработка истории статусов
                if (oldStatus != existingOrder.Status)
                {
                    var currentHistory = await _context.OrderStatusHistories
                        .FirstOrDefaultAsync(h => h.OrderId == id && h.EndTime == null);
                    if (currentHistory != null) currentHistory.EndTime = DateTime.UtcNow;

                    _context.OrderStatusHistories.Add(new AccuratSystem.Contracts.Models.OrderStatusHistories
                    {
                        OrderId = id,
                        Status = existingOrder.Status,
                        StartTime = DateTime.UtcNow,
                        UserId = existingOrder.AdminId
                    });

                    _context.OrderTimelineEntries.Add(new AccuratSystem.Contracts.Models.OrderTimelineEntry
                    {
                        OrderId = id,
                        EntryType = TimelineEntryType.StatusChanged,
                        Message = $"Статус изменен на: {existingOrder.Status}",
                        CreatedBy = await GetActorName(),  // ИСПРАВЛЕНО: было "Система", теперь реальный пользователь
                        Timestamp = DateTime.UtcNow
                    });
                }

                // 3. Обновление мойщиков (ИСПРАВЛЕНИЕ ОШИБКИ PK_OrderWashers)
                existingOrder.OrderWashers.Clear();

                if (order.OrderWashers != null && order.OrderWashers.Any())
                {
                    foreach (var ow in order.OrderWashers)
                    {
                        existingOrder.OrderWashers.Add(new OrderWasher
                        {
                            OrderId = id,
                            UserId = ow.UserId,
                            SplitShare = ow.SplitShare,
                            EarnedAmount = 0
                        });
                    }
                }

                // 4. РАСЧЕТ ЦЕНЫ И ЗАМОРОЗКА ЗП
                var branch = await _context.Branches.FindAsync(existingOrder.BranchId);
                var settings = await _context.CompanySettings.FindAsync(branch?.CompanyId ?? 0);
                var actualServices = await _context.Services.Where(s => existingOrder.ServiceIds.Contains(s.Id)).ToListAsync();
                var washers = await _context.Users.Where(u => existingOrder.OrderWashers.Select(ow => ow.UserId).Contains(u.Id)).ToListAsync();

                var finalCalc = OrderMath.Calculate(existingOrder, actualServices, washers, settings);
                existingOrder.FinalPrice = finalCalc.FinalPrice;

                existingOrder.TotalPrice = finalCalc.ServicesTotal;
                existingOrder.OriginalTotalPrice = finalCalc.ServicesTotal;

                if (existingOrder.Status == "Выполнен" || existingOrder.Status == "Завершен")
                {
                    foreach (var ow in existingOrder.OrderWashers)
                    {
                        ow.EarnedAmount = finalCalc.WasherEarnings * ow.SplitShare;
                    }
                }

                await _context.SaveChangesAsync();
                transaction.Commit();

                await _hubContext.Clients.All.SendAsync("UpdateData");
                return NoContent();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return StatusCode(500, "Ошибка обновления: " + ex.Message);
            }
        }

        #region Аудит финансовых изменений (хелперы контроллера)

        /// <summary>
        /// Аудит операционных изменений: бокс, мойщик, способ оплаты, время.
        /// Вызывается вместе с AuditPriceChanges.
        /// </summary>
        private async Task AuditOperationalChanges(Order oldOrder, Order newOrder)
        {
            var actor = await GetActorName();
            int.TryParse(Request.Headers["X-User-Id"].FirstOrDefault(), out int actorId);
            int? relatedId = actorId == 0 ? null : actorId;

            // 1. Смена бокса
            if (oldOrder.BoxNumber != newOrder.BoxNumber)
            {
                _context.OrderTimelineEntries.Add(MakeAuditEntry(oldOrder.Id, TimelineEntryType.BoxChanged,
                    $"🅿️ Бокс изменён: {oldOrder.BoxNumber} → {newOrder.BoxNumber}",
                    actor, relatedId));
            }

            // 2. Смена способа оплаты
            if ((oldOrder.PaymentMethod ?? "") != (newOrder.PaymentMethod ?? ""))
            {
                _context.OrderTimelineEntries.Add(MakeAuditEntry(oldOrder.Id, TimelineEntryType.PaymentMethodChanged,
                    $"💳 Способ оплаты: {oldOrder.PaymentMethod ?? "не указан"} → {newOrder.PaymentMethod ?? "не указан"}",
                    actor, relatedId));
            }

            // 3. Смена времени записи
            if (oldOrder.Time != newOrder.Time)
            {
                _context.OrderTimelineEntries.Add(MakeAuditEntry(oldOrder.Id, TimelineEntryType.TimeChanged,
                    $"🕐 Время записи: {oldOrder.Time:dd.MM HH:mm} → {newOrder.Time:dd.MM HH:mm}",
                    actor, relatedId));
            }

            // 4. Смена мойщиков (сравниваем списки UserId)
            var oldWashers = oldOrder.OrderWashers?.Select(ow => ow.UserId).OrderBy(x => x).ToList() ?? new List<int>();
            var newWashers = newOrder.OrderWashers?.Select(ow => ow.UserId).OrderBy(x => x).ToList() ?? new List<int>();

            if (!oldWashers.SequenceEqual(newWashers))
            {
                var added = newWashers.Except(oldWashers).ToList();
                var removed = oldWashers.Except(newWashers).ToList();

                // Загружаем имена сотрудников для читаемости
                var allUserIds = added.Concat(removed).Distinct().ToList();
                var users = await _context.Users.Where(u => allUserIds.Contains(u.Id)).ToListAsync();

                string GetName(int id) => users.FirstOrDefault(u => u.Id == id)?.FullName ?? $"ID {id}";

                var parts = new List<string>();
                if (removed.Any()) parts.Add($"убраны: {string.Join(", ", removed.Select(GetName))}");
                if (added.Any()) parts.Add($"добавлены: {string.Join(", ", added.Select(GetName))}");

                _context.OrderTimelineEntries.Add(MakeAuditEntry(oldOrder.Id, TimelineEntryType.WasherChanged,
                    $"👤 Мойщики: {string.Join("; ", parts)}",
                    actor, relatedId));
            }
        }

        /// <summary>
        /// Возвращает имя пользователя из БД по ID из заголовка.
        /// Это безопасно: сервер берёт реальное имя из БД, а не доверяет клиенту.
        /// </summary>
        private async Task<string> GetActorName()
        {
            var idHeader = Request.Headers["X-User-Id"].FirstOrDefault();

            if (int.TryParse(idHeader, out int userId))
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                    return $"{user.FullName} (ID {userId})";
            }

            return "Неизвестный пользователь";
        }

        private static OrderTimelineEntry MakeAuditEntry(int orderId, TimelineEntryType type, string message, string actor, int? actorId)
        {
            return new OrderTimelineEntry
            {
                OrderId = orderId,
                EntryType = type,
                Message = message,
                CreatedBy = actor,
                Timestamp = DateTime.UtcNow,
                RelatedEntityId = actorId
            };
        }

        /// <summary>
        /// Сравнивает старое состояние заказа (из БД) с присланным клиентом и
        /// пишет в ленту событий все финансовые расхождения.
        /// ВАЖНО: вызывается ДО SetValues(), когда existingOrder ещё не изменён.
        /// </summary>
        private async Task AuditPriceChanges(Order oldOrder, Order newOrder)
        {
            var actor = await GetActorName();
            int.TryParse(Request.Headers["X-User-Id"].FirstOrDefault(), out int actorId);
            int? relatedId = actorId == 0 ? null : actorId;

            // 1. Скидка: процент или фиксированная сумма
            bool discountChanged = oldOrder.DiscountPercent != newOrder.DiscountPercent ||
                                   oldOrder.DiscountAmount != newOrder.DiscountAmount;
            if (discountChanged)
            {
                string OldDisp() => oldOrder.DiscountPercent > 0
                    ? $"{oldOrder.DiscountPercent:N0}%"
                    : (oldOrder.DiscountAmount > 0 ? $"{oldOrder.DiscountAmount:N0} ₽" : "нет");
                string NewDisp() => newOrder.DiscountPercent > 0
                    ? $"{newOrder.DiscountPercent:N0}%"
                    : (newOrder.DiscountAmount > 0 ? $"{newOrder.DiscountAmount:N0} ₽" : "нет");

                _context.OrderTimelineEntries.Add(MakeAuditEntry(oldOrder.Id, TimelineEntryType.DiscountApplied,
                    $"🏷 Скидка: {OldDisp()} → {NewDisp()}", actor, relatedId));
            }

            // 2. Доплата: сумма или причина (причина без суммы тоже пишется — это подозрительно)
            bool extraChanged = oldOrder.ExtraCost != newOrder.ExtraCost ||
                               (oldOrder.ExtraCostReason ?? "") != (newOrder.ExtraCostReason ?? "");
            if (extraChanged)
            {
                _context.OrderTimelineEntries.Add(MakeAuditEntry(oldOrder.Id, TimelineEntryType.ExtraCostChanged,
                    $"➕ Доплата: {oldOrder.ExtraCost:N0} ₽ → {newOrder.ExtraCost:N0} ₽. Причина: «{newOrder.ExtraCostReason}»",
                    actor, relatedId));
            }

            // 3. Список услуг (влияет на итоговую цену)
            var oldServices = (oldOrder.ServiceIds ?? new List<int>()).OrderBy(x => x).ToList();
            var newServices = (newOrder.ServiceIds ?? new List<int>()).OrderBy(x => x).ToList();
            if (!oldServices.SequenceEqual(newServices))
            {
                var added = newServices.Except(oldServices).ToList();
                var removed = oldServices.Except(newServices).ToList();
                var parts = new List<string>();
                if (removed.Any()) parts.Add($"убраны: {string.Join(", ", removed)}");
                if (added.Any()) parts.Add($"добавлены: {string.Join(", ", added)}");

                _context.OrderTimelineEntries.Add(MakeAuditEntry(oldOrder.Id, TimelineEntryType.PriceChanged,
                    $"🛒 Состав услуг изменён: {string.Join("; ", parts)}", actor, relatedId));
            }
        }

        #endregion

        #region Журнал аудита

        /// <summary>
        /// Возвращает все записи журнала действий с пагинацией и фильтрами.
        /// Для директора: все записи компании. Для админа: только своего филиала.
        /// </summary>
        [HttpGet("audit-log")]
        public async Task<ActionResult<IEnumerable<OrderTimelineEntry>>> GetAuditLog(
            [FromQuery] int? userId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string entryType = null,
            [FromQuery] int pageSize = 100,
            [FromQuery] int pageNumber = 1)
        {
            var query = _context.OrderTimelineEntries.AsQueryable();

            if (userId.HasValue)
            {
                query = query.Where(e => e.RelatedEntityId == userId.Value);
            }

            if (startDate.HasValue)
            {
                var startUtc = BusinessTime.ToInstantUtc(startDate.Value);
                query = query.Where(e => e.Timestamp >= startUtc);
            }
            if (endDate.HasValue)
            {
                var endUtc = BusinessTime.ToInstantUtc(endDate.Value.Date.AddDays(1).AddTicks(-1));
                query = query.Where(e => e.Timestamp <= endUtc);
            }

            if (!string.IsNullOrWhiteSpace(entryType))
            {
                if (Enum.TryParse<TimelineEntryType>(entryType, out var parsedType))
                {
                    query = query.Where(e => e.EntryType == parsedType);
                }
            }

            var entries = await query
                .OrderByDescending(e => e.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(entries);
        }

        #endregion


        [HttpPatch("{id}/complete")]
        public async Task<IActionResult> CompleteOrder(int id, [FromQuery] string paymentMethod)
        {
            // БЕЗОПАСНОСТЬ: Проверка владения заказом
            var order = await VerifyOrderAccess(id);
            if (order == null) return (CurrentCompanyId != 0) ? Forbid() : NotFound();

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    await _context.Entry(order).Collection(o => o.OrderWashers).LoadAsync();
                    var branch = await _context.Branches.FindAsync(order.BranchId);
                    var settings = await _context.CompanySettings.FindAsync(branch?.CompanyId ?? 0);
                    var services = await _context.Services.Where(s => order.ServiceIds.Contains(s.Id)).ToListAsync();
                    var washers = await _context.Users.Where(u => order.OrderWashers.Select(ow => ow.UserId).Contains(u.Id)).ToListAsync();

                    var finalCalc = OrderMath.Calculate(order, services, washers, settings);

                    foreach (var ow in order.OrderWashers) ow.EarnedAmount = finalCalc.WasherEarnings * ow.SplitShare;

                    order.Status = "Выполнен";
                    if (!string.IsNullOrWhiteSpace(paymentMethod)) order.PaymentMethod = paymentMethod;

                    var outboxMsg = new OutboxMessage
                    {
                        EventType = "OrderCompleted",
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(new { OrderId = order.Id, Total = order.FinalPrice }),
                        CreatedAtUtc = DateTime.UtcNow
                    };

                    _context.OutboxMessages.Add(outboxMsg);
                    await _context.SaveChangesAsync();
                    transaction.Commit();
                    await _hubContext.Clients.All.SendAsync("UpdateData");
                    return Ok(order);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return StatusCode(500, "Ошибка при завершении заказа: " + ex.Message);
                }
            }
        }

        [HttpGet("client/{clientId}")]
        public async Task<ActionResult<IEnumerable<Order>>> GetByClient(int clientId)
        {
            // БЕЗОПАСНОСТЬ: Проверяем, что клиент принадлежит этой компании
            var client = await _context.Clients.FindAsync(clientId);
            if (client == null) return NotFound();
            if (CurrentCompanyId != 0 && client.CompanyId != CurrentCompanyId) return Forbid();

            return await _context.Orders
                .Include(o => o.OrderWashers)
                .Where(o => o.ClientId == clientId)
                .ToListAsync();
        }

        [HttpGet("check-availability")]
        public async Task<ActionResult<bool>> Check(int branchId, int box, DateTime start, int duration, int? excludeOrderId = null)
        {
            // БЕЗОПАСНОСТЬ: Проверка доступа к филиалу
            if (!await VerifyBranchAccess(branchId)) return Forbid();

            var utcStart = BusinessTime.ToInstantUtc(start);
            var end = utcStart.AddMinutes(duration);

            var isBusy = await _context.Orders.AnyAsync(o =>
                o.BranchId == branchId &&
                o.BoxNumber == box &&
                o.Status != "Отменен" &&
                o.Id != excludeOrderId &&
                utcStart < o.Time.AddMinutes(o.DurationMinutes) &&
                end > o.Time);

            return Ok(!isBusy);
        }

        [HttpGet("active/{branchId}")]
        public async Task<ActionResult<IEnumerable<Order>>> GetActiveOrders(int branchId)
        {
            // БЕЗОПАСНОСТЬ: Проверка доступа к филиалу
            if (!await VerifyBranchAccess(branchId)) return Forbid();

            var activeOrders = await _context.Orders
                .Include(o => o.OrderWashers).ThenInclude(ow => ow.Washer)
                .Where(o => o.BranchId == branchId && o.Status == "В работе")
                .ToListAsync();

            return Ok(activeOrders);
        }

        [HttpPost("{id}/expenses")]
        public async Task<IActionResult> AddExpense(int id, [FromBody] AddOrderExpenseDto dto)
        {
            // БЕЗОПАСНОСТЬ: Проверка доступа к заказу
            var order = await VerifyOrderAccess(id);
            if (order == null) return (CurrentCompanyId != 0) ? Forbid() : NotFound();

            if (dto.CostPrice < 0 || dto.ClientPrice < 0) return BadRequest("Цены не могут быть отрицательными");

            var expense = new OrderExpense
            {
                OrderId = id,
                Name = dto.Name,
                Category = dto.Category,
                CostPrice = dto.CostPrice,
                ClientPrice = dto.ClientPrice,
                Quantity = dto.Quantity,
                Note = dto.Note,
                CreatedAt = DateTime.UtcNow
            };

            _context.OrderExpenses.Add(expense);
            await _context.SaveChangesAsync();

            string timelineMessage = $"Добавлен расход: {expense.Name} ({expense.ClientPrice * expense.Quantity:N0} ₽)";
            if (!string.IsNullOrWhiteSpace(expense.Note)) timelineMessage += $". Примечание: {expense.Note}";

            _context.OrderTimelineEntries.Add(new AccuratSystem.Contracts.Models.OrderTimelineEntry
            {
                OrderId = id,
                EntryType = TimelineEntryType.ExpenseAdded,
                Message = timelineMessage,
                CreatedBy = dto.CreatedByUser ?? "Система",
                Timestamp = DateTime.UtcNow,
                RelatedEntityId = expense.Id
            });
            await _context.SaveChangesAsync();

            return Ok(expense);
        }

        [HttpGet("{id}/timeline")]
        public async Task<IActionResult> GetTimeline(int id)
        {
            // БЕЗОПАСНОСТЬ: Проверка доступа
            if (await VerifyOrderAccess(id) == null) return (CurrentCompanyId != 0) ? Forbid() : NotFound();

            var entries = await _context.OrderTimelineEntries
                .Where(e => e.OrderId == id)
                .OrderByDescending(e => e.Timestamp)
                .ToListAsync();

            return Ok(entries);
        }

        [HttpPut("services/{id}/price")]
        public async Task<IActionResult> UpdateServicePrice(int id, [FromBody] UpdateServicePriceDto laPriceDto)
        {
            var serviceItem = await _context.OrderServiceItems.FindAsync(id);
            if (serviceItem == null) return NotFound();

            // БЕЗОПАСНОСТЬ: Проверяем через связь Order -> Branch -> Company
            var order = await _context.Orders.FindAsync(serviceItem.OrderId);
            if (order == null) return NotFound();

            var branch = await _context.Branches.FindAsync(order.BranchId);
            if (CurrentCompanyId != 0 && branch?.CompanyId != CurrentCompanyId) return Forbid();

            var oldPrice = serviceItem.ActualPrice;
            serviceItem.ActualPrice = laPriceDto.NewPrice;
            serviceItem.PriceNote = laPriceDto.Note;

            _context.OrderServiceItems.Update(serviceItem);

            _context.OrderTimelineEntries.Add(new AccuratSystem.Contracts.Models.OrderTimelineEntry
            {
                OrderId = serviceItem.OrderId,
                EntryType = TimelineEntryType.PriceChanged,
                Message = $"Цена изменена: {oldPrice:N0} ₽ → {laPriceDto.NewPrice:N0} ₽. {laPriceDto.Note}",
                CreatedBy = laPriceDto.UpdatedByUser ?? "System",
                Timestamp = DateTime.UtcNow,
                RelatedEntityId = serviceItem.Id
            });
            await _context.SaveChangesAsync();
            return Ok(serviceItem);
        }

        [HttpGet("{id}/expenses")]
        public async Task<IActionResult> GetExpenses(int id)
        {
            // БЕЗОПАСНОСТЬ: Проверка доступа
            if (await VerifyOrderAccess(id) == null) return (CurrentCompanyId != 0) ? Forbid() : NotFound();

            var expenses = await _context.OrderExpenses.Where(e => e.OrderId == id).ToListAsync();
            return Ok(expenses);
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> ChangeStatus(int id, [FromBody] AccuratSystem.Contracts.DTOs.ChangeStatusDto dto)
        {
            // БЕЗОПАСНОСТЬ: Проверка доступа
            var order = await VerifyOrderAccess(id);
            if (order == null) return (CurrentCompanyId != 0) ? Forbid() : NotFound();

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var currentHistory = await _context.OrderStatusHistories.FirstOrDefaultAsync(h => h.OrderId == id && h.EndTime == null);
                    if (currentHistory != null) currentHistory.EndTime = DateTime.UtcNow;

                    order.Status = dto.NewStatus;

                    _context.OrderStatusHistories.Add(new AccuratSystem.Contracts.Models.OrderStatusHistories
                    {
                        OrderId = id,
                        Status = dto.NewStatus,
                        StartTime = DateTime.UtcNow,
                        UserId = dto.UserId
                    });

                    _context.OrderTimelineEntries.Add(new AccuratSystem.Contracts.Models.OrderTimelineEntry
                    {
                        OrderId = id,
                        EntryType = TimelineEntryType.StatusChanged,
                        Message = $"Статус изменен на: {dto.NewStatus}",
                        CreatedBy = !string.IsNullOrEmpty(dto.UserName) ? dto.UserName : "Система",
                        Timestamp = DateTime.UtcNow
                    });

                    await _context.SaveChangesAsync();
                    transaction.Commit();
                    await _hubContext.Clients.All.SendAsync("UpdateData");
                    return Ok(new { message = "Статус успешно изменен", status = dto.NewStatus });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return StatusCode(500, new { message = "Ошибка сервера", error = ex.Message });
                }
            }
        }

        [HttpGet("{id}/time-analysis")]
        public async Task<IActionResult> GetTimeAnalysis(int id)
        {
            // БЕЗОПАСНОСТЬ: Проверка доступа
            if (await VerifyOrderAccess(id) == null) return (CurrentCompanyId != 0) ? Forbid() : NotFound();

            var history = await _context.OrderStatusHistories
                .Where(h => h.OrderId == id)
                .OrderBy(h => h.StartTime)
                .ToListAsync();

            if (history == null || !history.Any()) return NotFound(new { message = "История времени не найдена" });

            var analysis = history.Select(h => new
            {
                h.Status,
                DurationTicks = (h.EndTime ?? DateTime.UtcNow) - h.StartTime
            }).ToList();

            var summary = analysis.GroupBy(a => a.Status)
                .Select(g => new
                {
                    Status = g.Key,
                    TotalDuration = TimeSpan.FromTicks(g.Sum(x => x.DurationTicks.Ticks)),
                    Occurrences = g.Count()
                }).ToList();

            return Ok(summary);
        }

        
    }
}
