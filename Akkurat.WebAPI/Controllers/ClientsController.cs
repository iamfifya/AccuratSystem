using AccuratSystem.Contracts.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Accurat.WebAPI.Data;

namespace Accurat.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ClientsController(AppDbContext context)
        {
            _context = context;
        }

        // Читаем заголовок из WPF
        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id)
            ? int.Parse(id)
            : 1;

        // 1. Получить список клиентов (ТОЛЬКО СВОИХ)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Client>>> GetClients()
        {
            return await _context.Clients
                .Where(c => CurrentCompanyId == 0 || c.CompanyId == CurrentCompanyId)
                .ToListAsync();
        }

        // 2. СУПЕР-ФИЧА: Найти клиента по части номера
        [HttpGet("number/{carNumber}")]
        public async Task<ActionResult<Client>> GetByNumber(string carNumber)
        {
            // Ищем только по своей компании (или везде, если Разработчик)
            var client = await _context.Clients
                .Where(c => CurrentCompanyId == 0 || c.CompanyId == CurrentCompanyId)
                .FirstOrDefaultAsync(c => c.CarNumber.ToLower().Contains(carNumber.ToLower()));

            if (client == null)
            {
                return NotFound(new { message = "Клиент с таким номером не найден" });
            }

            return Ok(client);
        }

        // 3. Добавить нового клиента
        [HttpPost]
        public async Task<ActionResult<Client>> CreateClient(Client client)
        {
            // ЖЕСТКАЯ ПРИВЯЗКА: Привязываем клиента к компании того, кто его создал
            client.CompanyId = CurrentCompanyId == 0 ? 1 : CurrentCompanyId;

            client.RegistrationDate = DateTime.UtcNow;
            client.VisitsCount = 0;
            client.TotalSpent = 0;

            _context.Clients.Add(client);
            await _context.SaveChangesAsync();

            return Ok(client);
        }

        // 4. Обновить данные клиента
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateClient(int id, Client client)
        {
            if (id != client.Id) return BadRequest(new { message = "ID в URL и в объекте не совпадают" });

            // Опционально: можно добавить проверку, что клиент принадлежит компании
            _context.Entry(client).State = EntityState.Modified;

            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Clients.Any(c => c.Id == id)) return NotFound(new { message = "Клиент не найден в базе" });
                throw;
            }

            return NoContent();
        }

        // 5. Удалить клиента
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteClient(int id)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client == null) return NotFound(new { message = "Клиент не найден" });

            // Запрещаем удалять чужих клиентов
            if (CurrentCompanyId != 0 && client.CompanyId != CurrentCompanyId)
            {
                return Forbid();
            }

            _context.Clients.Remove(client);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}