using Accurat.WebAPI.Models;
using AccuratSystem.Contracts.Models;
using AccuratSystem.Contracts.Enums;
using AccuratSystem.Contracts.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Accurat.WebAPI.Data;
using Accurat.WebAPI.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Accurat.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<AppHub> _hubContext;

        public OrdersController(AppDbContext context, IHubContext<AppHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // 1. Получить все заказы
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrders()
        {
            // 1. Загружаем заказы. Благодаря .Ignore() в DbContext, EF не будет искать CurrentStatusStartTime в БД
            var orders = await _context.Orders.Include(o => o.OrderWashers).ToListAsync();

            foreach (var order in orders)
            {
                // 2. Теперь мы вручную идем в таблицу истории и берем дату начала текущего статуса
                var latestHistory = await _context.OrderStatusHistories
                    .FirstOrDefaultAsync(h => h.OrderId == order.Id && h.EndTime == null);

                // Записываем дату в свойство, которое теперь "невидимо" для БД, но доступно для нас
                order.CurrentStatusStartTime = latestHistory?.StartTime;
            }

            return orders;
        }



        // 2. Создать новый заказ (С АВТОМАТИЧЕСКИМ СТАРТОМ ВРЕМЕНИ)
        [HttpPost]
        public async Task<ActionResult<Order>> CreateOrder(Order order)
        {
            if (order.Status == "Выполнен" && (string.IsNullOrWhiteSpace(order.PaymentMethod) || order.PaymentMethod == "Не указано"))
            {
                return BadRequest("Для выполненного заказа требуется указать способ оплаты.");
            }

            using (var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    if (order.OrderWashers == null) order.OrderWashers = new List<OrderWasher>();
                    foreach (var ow in order.OrderWashers) { ow.OrderId = order.Id; }

                    var endTime = order.Time.AddMinutes(order.DurationMinutes > 0 ? order.DurationMinutes : 60);

                    bool hasConflict = await _context.Orders.AnyAsync(o =>
                        o.BoxNumber == order.BoxNumber &&
                        o.BranchId == order.BranchId &&
                        o.Status != "Отменен" &&
                        o.Status != "Выполнен" &&
                        o.Time < endTime &&
                        o.Time.AddMinutes(o.DurationMinutes > 0 ? o.DurationMinutes : 60) > order.Time);

                    if (hasConflict) return BadRequest(new { message = "Выбранное время в данном боксе уже занято" });

                    order.Time = DateTime.SpecifyKind(order.Time, DateTimeKind.Utc);

                    // Если статус не указан, ставим "В работе"
                    if (string.IsNullOrEmpty(order.Status)) order.Status = "В работе";

                    _context.Orders.Add(order);
                    await _context.SaveChangesAsync(); // Сначала сохраняем заказ, чтобы получить его Id

                    // НОВОЕ: Создаем первую запись в истории времени
                    var firstHistory = new AccuratSystem.Contracts.Models.OrderStatusHistory
                    {
                        OrderId = order.Id,
                        Status = order.Status,
                        StartTime = DateTime.UtcNow,
                        UserId = order.OrderWashers.FirstOrDefault()?.UserId
                    };
                    _context.OrderStatusHistories.Add(firstHistory);
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

        [HttpPost("{id}/convert")]
        public async Task<ActionResult<Order>> ConvertToOrder(int id, [FromQuery] int shiftId, [FromQuery] int washerId)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // 1. Ищем заказ (запись) и обязательно подтягиваем OrderWashers
                    var order = await _context.Orders
                        .Include(o => o.OrderWashers)
                        .FirstOrDefaultAsync(o => o.Id == id);

                    if (order == null)
                        return NotFound($"Запись с ID {id} не найдена в БД.");

                    // 2. Снимаем флаги предварительной записи
                    order.IsAppointment = false;
                    order.Status = "В работе";
                    order.ShiftId = shiftId;
                    order.Time = DateTime.UtcNow; // Фиксируем реальное время заезда в бокс

                    // 3. 💥 ЖЕЛЕЗНО НАЗНАЧАЕМ МОЙЩИКА 💥
                    order.OrderWashers.Clear();
                    order.OrderWashers.Add(new OrderWasher
                    {
                        OrderId = order.Id,
                        UserId = washerId,
                        SplitShare = 1.0m
                    });

                    // 4. Фиксируем время старта работы в истории статусов
                    var newHistory = new AccuratSystem.Contracts.Models.OrderStatusHistory
                    {
                        OrderId = order.Id,
                        Status = "В работе",
                        StartTime = DateTime.UtcNow,
                        UserId = washerId
                    };
                    _context.OrderStatusHistories.Add(newHistory);

                    // 5. Делаем отметку в ленте событий
                    var timelineEntry = new AccuratSystem.Contracts.Models.OrderTimelineEntry
                    {
                        OrderId = order.Id,
                        EntryType = TimelineEntryType.StatusChanged,
                        Message = "Предварительная запись переведена в работу",
                        CreatedBy = "Система (Конвертация)",
                        Timestamp = DateTime.UtcNow
                    };
                    _context.OrderTimelineEntries.Add(timelineEntry);

                    // 6. Сохраняем всё в базу
                    _context.Entry(order).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                    transaction.Commit();

                    // 7. Обновляем UI у всех админов
                    await _hubContext.Clients.All.SendAsync("UpdateData");

                    return Ok(order);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return StatusCode(500, "Ошибка конвертации на сервере: " + ex.Message);
                }
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOrder(int id, Order order)
        {
            if (id != order.Id) return BadRequest();

            if (order.Status == "Выполнен" && (string.IsNullOrWhiteSpace(order.PaymentMethod) || order.PaymentMethod == "Не указано"))
            {
                return BadRequest("Для выполненного заказа требуется указать способ оплаты.");
            }

            order.Time = DateTime.SpecifyKind(order.Time, DateTimeKind.Utc);

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // ИСПРАВЛЕНИЕ: Добавлено .AsNoTracking(). 
                    // EF просто считает данные для проверки статуса, но не заблокирует ID в памяти.
                    var existingOrder = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
                    if (existingOrder == null) return NotFound();

                    // Если статус изменился, фиксируем это в истории времени
                    if (existingOrder.Status != order.Status)
                    {
                        var currentHistory = await _context.OrderStatusHistories
                            .FirstOrDefaultAsync(h => h.OrderId == id && h.EndTime == null);
                        if (currentHistory != null) currentHistory.EndTime = DateTime.UtcNow;

                        var newHistory = new AccuratSystem.Contracts.Models.OrderStatusHistory
                        {
                            OrderId = id,
                            Status = order.Status,
                            StartTime = DateTime.UtcNow
                        };
                        _context.OrderStatusHistories.Add(newHistory);
                    }

                    // Теперь EF без проблем примет обновленный заказ от WPF
                    _context.Entry(order).State = EntityState.Modified;
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
        }


        [HttpPatch("{id}/complete")]
        public async Task<IActionResult> CompleteOrder(int id, [FromQuery] string paymentMethod)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var order = await _context.Orders.FindAsync(id);
                    if (order == null) return NotFound();

                    order.Status = "Выполнен";

                    if (!string.IsNullOrWhiteSpace(paymentMethod))
                    {
                        order.PaymentMethod = paymentMethod;
                    }

                    var payload = new { OrderId = order.Id, ClientId = order.ClientId, Total = order.FinalPrice };

                    var outboxMsg = new OutboxMessage
                    {
                        EventType = "OrderCompleted",
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(payload),
                        CreatedAtUtc = DateTime.UtcNow,
                        ErrorMessage = ""
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
            return await _context.Orders
                .Include(o => o.OrderWashers)
                .Where(o => o.ClientId == clientId)
                .ToListAsync();
        }

        [HttpGet("check-availability")]
        public async Task<ActionResult<bool>> Check(int branchId, int box, DateTime start, int duration, int? excludeOrderId = null)
        {
            var utcStart = DateTime.SpecifyKind(start, DateTimeKind.Utc);
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
            var activeOrders = await _context.Orders
                .Include(o => o.OrderWashers)
                .Where(o => o.BranchId == branchId && o.Status == "В работе")
                .ToListAsync();

            return Ok(activeOrders);
        }

        [HttpPost("{id}/expenses")]
        public async Task<IActionResult> AddExpense(int id, [FromBody] AddOrderExpenseDto dto)
        {
            if (dto.CostPrice < 0 || dto.ClientPrice < 0)
                return BadRequest("Цены не могут быть отрицательными");

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

            var timelineEntry = new AccuratSystem.Contracts.Models.OrderTimelineEntry
            {
                OrderId = id,
                EntryType = TimelineEntryType.ExpenseAdded,
                Message = $"Добавлен расход: {expense.Name} ({expense.ClientPrice * expense.Quantity:N0} ₽)",
                CreatedBy = dto.CreatedByUser ?? "Система",
                Timestamp = DateTime.UtcNow,
                RelatedEntityId = expense.Id
            };
            _context.OrderTimelineEntries.Add(timelineEntry);
            await _context.SaveChangesAsync();

            return Ok(expense);
        }

        [HttpGet("{id}/timeline")]
        public async Task<IActionResult> GetTimeline(int id)
        {
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

            var oldPrice = serviceItem.ActualPrice;
            serviceItem.ActualPrice = laPriceDto.NewPrice;
            serviceItem.PriceNote = laPriceDto.Note;

            _context.OrderServiceItems.Update(serviceItem);

            var timelineEntry = new AccuratSystem.Contracts.Models.OrderTimelineEntry
            {
                OrderId = serviceItem.OrderId,
                EntryType = TimelineEntryType.PriceChanged,
                Message = $"Цена изменена: {oldPrice:N0} ₽ → {laPriceDto.NewPrice:N0} ₽. {laPriceDto.Note}",
                CreatedBy = laPriceDto.UpdatedByUser ?? "System",
                Timestamp = DateTime.UtcNow,
                RelatedEntityId = serviceItem.Id
            };
            _context.OrderTimelineEntries.Add(timelineEntry);
            await _context.SaveChangesAsync();
            return Ok(serviceItem);
        }

        [HttpGet("{id}/expenses")]
        public async Task<IActionResult> GetExpenses(int id)
        {
            var expenses = await _context.OrderExpenses
                .Where(e => e.OrderId == id)
                .ToListAsync();

            return Ok(expenses);
        }

        // === ПРОФЕССИОНАЛЬНЫЙ УЧЕТ ВРЕМЕНИ ===

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> ChangeStatus(int id, [FromBody] AccuratSystem.Contracts.DTOs.ChangeStatusDto dto)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var order = await _context.Orders.FindAsync(id);
                    if (order == null) return NotFound(new { message = "Заказ не найден" });

                    var currentHistory = await _context.OrderStatusHistories
                        .FirstOrDefaultAsync(h => h.OrderId == id && h.EndTime == null);

                    if (currentHistory != null)
                    {
                        currentHistory.EndTime = DateTime.UtcNow;
                    }

                    order.Status = dto.NewStatus;
                    _context.Orders.Update(order);

                    var newHistory = new AccuratSystem.Contracts.Models.OrderStatusHistory
                    {
                        OrderId = id,
                        Status = dto.NewStatus,
                        StartTime = DateTime.UtcNow,
                        UserId = dto.UserId
                    };
                    _context.OrderStatusHistories.Add(newHistory);

                    var timelineEntry = new AccuratSystem.Contracts.Models.OrderTimelineEntry
                    {
                        OrderId = id,
                        EntryType = TimelineEntryType.StatusChanged,
                        Message = $"Статус изменен на: {dto.NewStatus}",
                        CreatedBy = !string.IsNullOrEmpty(dto.UserName) ? dto.UserName : "Система",
                        Timestamp = DateTime.UtcNow
                    };
                    _context.OrderTimelineEntries.Add(timelineEntry);

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
            var history = await _context.OrderStatusHistories
                .Where(h => h.OrderId == id)
                .OrderBy(h => h.StartTime)
                .ToListAsync();

            if (history == null || !history.Any())
                return NotFound(new { message = "История времени для этого заказа не найдена" });

            var analysis = history.Select(h => new
            {
                h.Status,
                DurationTicks = (h.EndTime ?? DateTime.UtcNow) - h.StartTime
            }).ToList();

            var summary = analysis.GroupBy(a => a.Status)
                .Select(g => new
                {
                    Status = g.Key,
                    // Считаем сумму тиков (long), а затем переводим обратно в TimeSpan
                    TotalDuration = TimeSpan.FromTicks(g.Sum(x => x.DurationTicks.Ticks)),
                    Occurrences = g.Count()
                }).ToList();

            return Ok(summary);
        }
    }
}
