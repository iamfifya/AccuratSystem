using Accurat.WebAPI.Data;
using Accurat.WebAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            // 🔥 ДОБАВЛЕН INCLUDE
            return await _context.Orders.Include(o => o.OrderWashers).ToListAsync();
        }

        // 2. Создать новый заказ (С ЗАЩИТОЙ ОТ ДВОЙНОГО БРОНИРОВАНИЯ)
        [HttpPost]
        public async Task<ActionResult<Order>> CreateOrder(Order order) // ИСПРАВЛЕНО: было CarWashOrder
        {
            if (order.Status == "Выполнен" && (string.IsNullOrWhiteSpace(order.PaymentMethod) || order.PaymentMethod == "Не указано"))
            {
                return BadRequest("Для выполненного заказа требуется указать способ оплаты.");
            }

            // Открываем строгую транзакцию
            using (var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    // 1. Проверяем пересечения по времени для данного бокса
                    var endTime = order.Time.AddMinutes(order.DurationMinutes > 0 ? order.DurationMinutes : 60);

                    bool hasConflict = await _context.Orders.AnyAsync(o =>
                        o.BoxNumber == order.BoxNumber &&
                        o.BranchId == order.BranchId &&
                        o.Status != "Отменен" &&
                        o.Status != "Выполнен" &&
                        o.Time < endTime &&
                        // Если у заказа в БД DurationMinutes 0, считаем как 60
                        o.Time.AddMinutes(o.DurationMinutes > 0 ? o.DurationMinutes : 60) > order.Time);

                    if (hasConflict)
                    {
                        return BadRequest(new { message = "Выбранное время в данном боксе уже занято" });
                    }

                    // 2. Инициализируем Soft Split. Чистая инициализация: если вдруг прилетела пустышка, создаем пустой список
                    if (order.OrderWashers == null)
                    {
                        order.OrderWashers = new List<OrderWasher>();
                    }

                    order.Time = DateTime.SpecifyKind(order.Time, DateTimeKind.Utc);
                    order.Status = "В работе";

                    _context.Orders.Add(order);
                    await _context.SaveChangesAsync(); // Сохраняем в БД

                    transaction.Commit(); // Фиксируем транзакцию

                    // Уведомляем клиентов (WPF) об обновлении
                    await _hubContext.Clients.All.SendAsync("UpdateData");

                    return Ok(order);
                }
                catch (DbUpdateException)
                {
                    // Перехват конфликтов БД
                    transaction.Rollback();
                    return BadRequest(new { message = "Произошел конфликт при сохранении. Выбранное время в данном боксе уже занято." });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return StatusCode(500, "Внутренняя ошибка сервера");
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

            // Обязательно спасаем время от ошибки PostgreSQL
            order.Time = DateTime.SpecifyKind(order.Time, DateTimeKind.Utc);

            _context.Entry(order).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("UpdateData");

            return NoContent();
        }

        // 3. Завершить заказ (с публикацией события в Outbox и сохранением оплаты)
        [HttpPatch("{id}/complete")]
        public async Task<IActionResult> CompleteOrder(int id, [FromQuery] string paymentMethod)
        {
            // Открываем транзакцию
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var order = await _context.Orders.FindAsync(id);
                    if (order == null) return NotFound();

                    // Меняем статус заказа
                    order.Status = "Выполнен";

                    // 🔥 ПРИМЕНЯЕМ СПОСОБ ОПЛАТЫ ОТ КЛИЕНТА
                    if (!string.IsNullOrWhiteSpace(paymentMethod))
                    {
                        order.PaymentMethod = paymentMethod;
                    }

                    // ФОРМИРУЕМ ЗАДАЧУ ДЛЯ ФОНОВОГО ПРОЦЕССА (Outbox)
                    var payload = new { OrderId = order.Id, ClientId = order.ClientId, Total = order.FinalPrice };

                    var outboxMsg = new OutboxMessage
                    {
                        EventType = "OrderCompleted",
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(payload),
                        CreatedAtUtc = DateTime.UtcNow,
                        ErrorMessage = "" // 🔥 ИСПРАВЛЕНО: Явно передаем пустую строку, чтобы успокоить PostgreSQL
                    };

                    _context.OutboxMessages.Add(outboxMsg);

                    _context.OutboxMessages.Add(outboxMsg);

                    await _context.SaveChangesAsync();
                    transaction.Commit(); // Подтверждаем транзакцию

                    // Сигнал для UI (чтобы WPF обновился мгновенно)
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
            // 🔥 ДОБАВЛЕН INCLUDE
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

            // Ищем пересечения. Формула пересечения интервалов: (StartA < EndB) и (EndA > StartB)
            var isBusy = await _context.Orders.AnyAsync(o =>
                o.BranchId == branchId &&
                o.BoxNumber == box &&
                o.Status != "Отменен" && // Отмененные записи/заказы не занимают бокс
                o.Id != excludeOrderId &&
                utcStart < o.Time.AddMinutes(o.DurationMinutes) &&
                end > o.Time);

            return Ok(!isBusy);
        }

        // GET: api/Orders/active/{branchId}
        [HttpGet("active/{branchId}")]
        public async Task<ActionResult<IEnumerable<Order>>> GetActiveOrders(int branchId)
        {
            // 🔥 ДОБАВЛЕН INCLUDE
            var activeOrders = await _context.Orders
                .Include(o => o.OrderWashers)
                .Where(o => o.BranchId == branchId && o.Status == "В работе")
                .ToListAsync();

            return Ok(activeOrders);
        }
    }
}