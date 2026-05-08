using Accurat.WebAPI.Data;
using Accurat.WebAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        // 1. Получить список всех клиентов
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Client>>> GetClients()
        {
            return await _context.Clients.ToListAsync();
        }

        // 2. СУПЕР-ФИЧА: Найти клиента по части номера
        [HttpGet("number/{carNumber}")] // 🔥 ИСПРАВЛЕНО: Теперь маршрут совпадает с ApiService
        public async Task<ActionResult<Client>> GetByNumber(string carNumber)
        {
            // 🔥 ИСПРАВЛЕНО: Используем Contains для поиска подстроки
            var client = await _context.Clients
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
            client.RegistrationDate = DateTime.UtcNow; // Автоматически ставим дату регистрации
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
            if (id != client.Id)
            {
                return BadRequest(new { message = "ID в URL и в объекте не совпадают" });
            }

            // Сообщаем Entity Framework, что объект изменен
            _context.Entry(client).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Clients.Any(c => c.Id == id))
                {
                    return NotFound(new { message = "Клиент не найден в базе" });
                }
                throw;
            }

            return NoContent(); // Успешно, без возврата данных
        }

        // 5. Удалить клиента
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteClient(int id)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client == null)
            {
                return NotFound(new { message = "Клиент не найден" });
            }

            _context.Clients.Remove(client);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}