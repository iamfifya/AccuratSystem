using AccuratSystem.Contracts.Models;
using AccuratSystem.Contracts.Enums;
using AccuratSystem.Contracts.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Accurat.WebAPI.Data;

namespace Accurat.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServicesController : ControllerBase
    {
        private readonly AppDbContext _context;
        public ServicesController(AppDbContext context) => _context = context;

        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id) ? int.Parse(id) : 1;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Service>>> GetServices()
        {
            // Отдаем услуги только текущей компании (или все, если Разработчик)
            return await _context.Services
                .Where(s => CurrentCompanyId == 0 || s.CompanyId == CurrentCompanyId)
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Service>> CreateService(Service service)
        {
            // Принудительно привязываем новую услугу к текущей компании
            service.CompanyId = CurrentCompanyId == 0 ? 1 : CurrentCompanyId;

            _context.Services.Add(service);
            await _context.SaveChangesAsync();
            return Ok(service);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateService(int id, Service service)
        {
            if (id != service.Id) return BadRequest();

            _context.Entry(service).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteService(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null) return NotFound();

            _context.Services.Remove(service);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}