
using AccuratSystem.Contracts.Models;
using AccuratSystem.Contracts.Enums;
using AccuratSystem.Contracts.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Accurat.WebAPI.Data;
using Accurat.WebAPI.Hubs;
using Microsoft.AspNetCore.SignalR;
using Accurat.WebAPI.Services; // ПОДКЛЮЧИЛИ НАШИ СЕРВИСЫ С ORDERMATH

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

        // ==========================================
        // НОВЫЙ ЭНДПОИНТ: СИНХРОННЫЙ API-КАЛЬКУЛЯТОР PREVIEW
        // ==========================================
        [HttpPost("calculate-preview")]
        public async Task<ActionResult<OrderCalculation>> CalculatePreview([FromBody] OrderPreviewRequestDto request)
        {
            try
            {
                // 1. Ищем филиал, чтобы через него выйти на настройки компании (тенанта)
                var branch = await _context.Branches.FindAsync(request.BranchId);
                if (branch == null) return BadRequest(new { message = "Филиал не найден" });

                var settings = await _context.CompanySettings.FindAsync(branch.CompanyId);

                // 2. Вытаскиваем актуальные услуги напрямую из базы данных
                var services = await _context.Services
                    .Where(s => request.ServiceIds.Contains(s.Id))
                    .ToListAsync();

                // 3. Вытаскиваем мойщика (если он выбран), чтобы учесть его персональный процент
                var washers = new List<User>();
                if (request.WasherId > 0)
                {
                    var washer = await _context.Users.FindAsync(request.WasherId);
                    if (washer != null) washers.Add(washer);
                }

                // 4. Генерируем виртуальный заказ для передачи в математическое ядро
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

                // Назначаем мойщика в коллекцию OrderWashers, если он передан
                if (request.WasherId > 0)
                {
                    virtualOrder.OrderWashers = new List<OrderWasher>
                    {
                        new OrderWasher { OrderId = 0, UserId = request.WasherId, SplitShare = 1.0m }
                    };
                }

                // 5. ВЫЗОВ РАСЧЕТА: теперь передаем request.ShiftType
                var calculation = OrderMath.Calculate(virtualOrder, services, washers, settings, request.ShiftType);

                return Ok(calculation);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка калькулятора на сервере: " + ex.Message });
            }
        }

        // 1. Получить все заказы
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrders([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            // 1. Устанавливаем временное окно. 
            // Если даты не передали, берем период: от "вчера" до "через месяц" 
            // (чтобы захватить и старые заказы смены, и будущие записи)
            DateTime start = startDate ?? DateTime.UtcNow.AddDays(-1);
            DateTime end = endDate ?? DateTime.UtcNow.AddDays(30);

            // 2. Создаем запрос. 
            // ВАЖНО: используем AsQueryable(), чтобы фильтрация прошла в самой базе данных, а не в памяти сервера
            var query = _context.Orders.AsQueryable();

            // Изоляция тенанта (SaaS)
            if (CurrentCompanyId != 0)
            {
                var myBranchIds = _context.Branches
                    .Where(b => b.CompanyId == CurrentCompanyId)
                    .Select(b => b.Id);
                query = query.Where(o => myBranchIds.Contains(o.BranchId));
            }

            // ФИЛЬТР ПО ДАТЕ (То самое спасение от "Смерти от данных")
            query = query.Where(o => o.Time >= start && o.Time <= end);

            // Теперь выгружаем только отфильтрованный список
            var orders = await query.Include(o => o.OrderWashers).ToListAsync();

            // Добавляем время последнего статуса (оставляем как было)
            foreach (var order in orders)
            {
                var latestHistory = await _context.OrderStatusHistories
                    .FirstOrDefaultAsync(h => h.OrderId == order.Id && h.EndTime == null);
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

                    if (string.IsNullOrEmpty(order.Status)) order.Status = "В работе";

                    // ЗАЩИТА: Пересчитываем финансовые показатели на бэкенде перед сохранением!
                    // Берем услуги, настройки и пересчитываем FinalPrice, чтобы клиент не прислал фейк.
                    var branch = await _context.Branches.FindAsync(order.BranchId);
                    var settings = await _context.CompanySettings.FindAsync(branch?.CompanyId ?? 0);
                    var services = await _context.Services.Where(s => order.ServiceIds.Contains(s.Id)).ToListAsync();
                    var washers = await _context.Users.Where(u => order.OrderWashers.Select(ow => ow.UserId).Contains(u.Id)).ToListAsync();

                    var finalCalc = OrderMath.Calculate(order, services, washers, settings);
                    order.FinalPrice = finalCalc.FinalPrice; // Железно пишем серверную цену

                    _context.Orders.Add(order);
                    await _context.SaveChangesAsync();

                    var firstHistory = new AccuratSystem.Contracts.Models.OrderStatusHistory
                    {
                        OrderId = order.Id,
                        Status = order.Status,
                        StartTime = DateTime.UtcNow,
                        UserId = order.OrderWashers.FirstOrDefault()?.UserId
                    };
                    
                    // 1. Инициализируем коллекцию, если она пуста
                    if (order.OrderServiceItems == null)
                        order.OrderServiceItems = new List<OrderServiceItem>();

                    // 2. Очищаем старые снапшоты (если это обновление)
                    _context.OrderServiceItems.RemoveRange(_context.OrderServiceItems.Where(osi => osi.OrderId == order.Id));

                    // 3. Создаем новые снапшоты с ТЕКУЩИМИ ценами из прайса
                    foreach (var serviceId in order.ServiceIds)
                    {
                        var service = await _context.Services.FindAsync(serviceId);
                        if (service != null)
                        {
                            // Берем цену для текущего типа кузова (или дефолтную)
                            decimal currentPrice = service.PriceByBodyType.TryGetValue(order.BodyTypeCategory, out var p) ? p :
                                                   (service.PriceByBodyType.TryGetValue(1, out var def) ? def : 0);

                            order.OrderServiceItems.Add(new OrderServiceItem
                            {
                                OrderId = order.Id, // EF сам подставит после SaveChanges, если Id=0
                                ServiceId = serviceId,
                                ActualPrice = currentPrice, // ЗАМОРАЖИВАЕМ ЦЕНУ НА МОМЕНТ ПРОДАЖИ
                                Quantity = 1
                            });
                        }
                    }

                    _context.Orders.Add(order);
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
                    var order = await _context.Orders
                        .Include(o => o.OrderWashers)
                        .FirstOrDefaultAsync(o => o.Id == id);

                    if (order == null)
                        return NotFound($"Запись с ID {id} не найдена в БД.");

                    order.IsAppointment = false;
                    order.Status = "В работе";
                    order.ShiftId = shiftId;
                    order.Time = DateTime.UtcNow;

                    order.OrderWashers.Clear();
                    order.OrderWashers.Add(new OrderWasher
                    {
                        OrderId = order.Id,
                        UserId = washerId,
                        SplitShare = 1.0m
                    });

                    var newHistory = new AccuratSystem.Contracts.Models.OrderStatusHistory
                    {
                        OrderId = order.Id,
                        Status = "В работе",
                        StartTime = DateTime.UtcNow,
                        UserId = washerId
                    };
                    _context.OrderStatusHistories.Add(newHistory);

                    var timelineEntry = new AccuratSystem.Contracts.Models.OrderTimelineEntry
                    {
                        OrderId = order.Id,
                        EntryType = TimelineEntryType.StatusChanged,
                        Message = "Предварительная запись переведена в работу",
                        CreatedBy = "Система (Конвертация)",
                        Timestamp = DateTime.UtcNow
                    };
                    _context.OrderTimelineEntries.Add(timelineEntry);

                    // ПЕРЕСЧЕТ ПРИ КОНВЕРТАЦИИ:
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
                    return StatusCode(500, "Ошибка конвертации на сервере: " + ex.Message);
                }
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOrder(int id, Order order)
        {
            if (id != order.Id) return BadRequest("ID не совпадают");

            // 1. Проверяем статус (используем существующий поиск)
            var existingOrder = await _context.Orders.FindAsync(id);
            if (existingOrder == null) return NotFound();

            if (existingOrder.Status == "Выполнен" || existingOrder.Status == "Завершен")
            {
                return BadRequest("Нельзя редактировать выполненный или завершенный заказ.");
            }

            if (order.Status == "Выполнен" && (string.IsNullOrWhiteSpace(order.PaymentMethod) || order.PaymentMethod == "Не указано"))
            {
                return BadRequest("Для выполненного заказа требуется указать способ оплаты.");
            }

            order.Time = DateTime.SpecifyKind(order.Time, DateTimeKind.Utc);

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // ИСПРАВЛЕНО: Меняем имя переменной с existingOrder на originalOrder
                    var originalOrder = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
                    if (originalOrder == null) return NotFound();

                    // Теперь используем originalOrder для защиты данных
                    order.BranchId = originalOrder.BranchId;
                    order.ShiftId = originalOrder.ShiftId;
                    order.AdminId = originalOrder.AdminId;
                    order.FinishedAt = originalOrder.FinishedAt;
                    order.GeneralNotes = originalOrder.GeneralNotes;

                    // 1. Обработка истории статусов
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

                    // 2. РАСЧЕТ ЦЕНЫ И ЗАМОРОЗКА ЗП (Критически важный блок!)
                    var branch = await _context.Branches.FindAsync(order.BranchId);
                    var settings = await _context.CompanySettings.FindAsync(branch?.CompanyId ?? 0);
                    var services = await _context.Services.Where(s => order.ServiceIds.Contains(s.Id)).ToListAsync(); // ОШИБКА ТУТ БЫЛА (ServiceIds)

                    // ПРАВИЛЬНЫЙ ЗАПРОС УСЛУГ:
                    var actualServices = await _context.Services.Where(s => order.ServiceIds.Contains(s.Id)).ToListAsync();
                    var washers = await _context.Users.Where(u => order.OrderWashers.Select(ow => ow.UserId).Contains(u.Id)).ToListAsync();

                    // Считаем математику
                    var finalCalc = OrderMath.Calculate(order, actualServices, washers, settings);
                    order.FinalPrice = finalCalc.FinalPrice;

                    // !!! ВОТ ТУТ ПРОИСХОДИТ ЗАМОРОЗКА !!!
                    // Если заказ переходит в статус "Выполнен" или уже в нем находится
                    if (order.Status == "Выполнен" || order.Status == "Завершен")
                    {
                        if (order.OrderWashers != null)
                        {
                            foreach (var ow in order.OrderWashers)
                            {
                                ow.EarnedAmount = finalCalc.WasherEarnings * ow.SplitShare;
                            }
                        }
                    }

                    // 3. Обновление мойщиков
                    var oldWashers = await _context.OrderWashers.Where(ow => ow.OrderId == id).ToListAsync();
                    _context.OrderWashers.RemoveRange(oldWashers);

                    if (order.OrderWashers != null && order.OrderWashers.Any())
                    {
                        foreach (var ow in order.OrderWashers)
                        {
                            ow.OrderId = id;
                            ow.Washer = null;
                            _context.OrderWashers.Add(ow);
                        }
                    }

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
                    var order = await _context.Orders
                        .Include(o => o.OrderWashers)
                        .FirstOrDefaultAsync(o => o.Id == id);

                    if (order == null) return NotFound();

                    var branch = await _context.Branches.FindAsync(order.BranchId);
                    var settings = await _context.CompanySettings.FindAsync(branch?.CompanyId ?? 0);
                    var services = await _context.Services.Where(s => order.ServiceIds.Contains(s.Id)).ToListAsync();
                    var washers = await _context.Users.Where(u => order.OrderWashers.Select(ow => ow.UserId).Contains(u.Id)).ToListAsync();

                    var finalCalc = OrderMath.Calculate(order, services, washers, settings);

                    // ЗАМОРОЗКА: Записываем заработок каждому мойщику в момент выполнения
                    foreach (var ow in order.OrderWashers)
                    {
                        ow.EarnedAmount = finalCalc.WasherEarnings * ow.SplitShare;
                    }

                    order.Status = "Выполнен";
                    if (!string.IsNullOrWhiteSpace(paymentMethod))
                    {
                        order.PaymentMethod = paymentMethod;
                    }

                    // ИСПРАВЛЕНО: Создаем объект outboxMsg перед использованием
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
                    .ThenInclude(ow => ow.Washer) // Чтобы на карточках были имена мойщиков
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

            // Формируем текст для Ленты
            string timelineMessage = $"Добавлен расход: {expense.Name} ({expense.ClientPrice * expense.Quantity:N0} ₽)";

            // Если кассир ввел комментарий, просто приклеиваем его к сообщению Ленты!
            if (!string.IsNullOrWhiteSpace(expense.Note))
            {
                timelineMessage += $". Примечание: {expense.Note}";
            }

            var timelineEntry = new AccuratSystem.Contracts.Models.OrderTimelineEntry
            {
                OrderId = id,
                EntryType = TimelineEntryType.ExpenseAdded,
                Message = timelineMessage, // Передаем склеенный текст
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
                    TotalDuration = TimeSpan.FromTicks(g.Sum(x => x.DurationTicks.Ticks)),
                    Occurrences = g.Count()
                }).ToList();

            return Ok(summary);
        }
    }
}