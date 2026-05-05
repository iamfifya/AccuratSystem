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
            return await _context.Orders.ToListAsync();
        }

        // 2. Создать новый заказ
        [HttpPost]
        public async Task<ActionResult<Order>> CreateOrder(Order order)
        {
            if (order.Status == "Выполнен" && (string.IsNullOrWhiteSpace(order.PaymentMethod) || order.PaymentMethod == "Не указано"))
            {
                return BadRequest("Для выполненного заказа требуется указать способ оплаты.");
            }

            // Уважаем время клиента, но жестко приводим к UTC
            order.Time = DateTime.SpecifyKind(order.Time, DateTimeKind.Utc);
            order.Status = "В работе";

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("UpdateData");

            return Ok(order);
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

        // 3. Завершить заказ (меняем статус)
        [HttpPatch("{id}/complete")]
        public async Task<IActionResult> CompleteOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            //  СТАВИМ ПРАВИЛЬНЫЙ СТАТУС, ЧТОБЫ РАБОТАЛА БУХГАЛТЕРИЯ 
            order.Status = "Выполнен";

            // Если нужно, чтобы по умолчанию ставилась безналичная оплата или что-то еще,
            // это тоже можно сделать здесь, но пока просто фиксируем статус.

            await _context.SaveChangesAsync();

            // Сигнал для WPF
            await _hubContext.Clients.All.SendAsync("UpdateData");

            return Ok(order);
        }

        [HttpGet("client/{clientId}")]
        public async Task<ActionResult<IEnumerable<Order>>> GetByClient(int clientId)
        {
            return await _context.Orders
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
    }
}