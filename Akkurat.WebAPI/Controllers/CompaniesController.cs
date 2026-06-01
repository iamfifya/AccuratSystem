using Accurat.WebAPI.Data;
using AccuratSystem.Contracts.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accurat.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CompaniesController : ControllerBase
    {
        private readonly AppDbContext _context;
        public CompaniesController(AppDbContext context) => _context = context;

        // Читаем заголовок из WPF
        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id) ? int.Parse(id) : 1;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Company>>> GetCompanies()
        {
            // Разработчик видит все компании. Обычный директор — только свою.
            if (CurrentCompanyId == 0)
                return await _context.Companies.ToListAsync();

            var myCompany = await _context.Companies.FindAsync(CurrentCompanyId);
            return myCompany != null ? new List<Company> { myCompany } : new List<Company>();
        }

        [HttpPost]
        public async Task<ActionResult<Company>> CreateCompany(Company company)
        {
            // БЕЗОПАСНОСТЬ: Только Разработчик может заводить новых клиентов в SaaS
            if (CurrentCompanyId != 0) return Forbid("Только Разработчик может создавать новые компании.");

            company.RegistrationDate = DateTime.UtcNow;
            _context.Companies.Add(company);
            await _context.SaveChangesAsync();
            return Ok(company);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCompany(int id, Company company)
        {
            if (id != company.Id) return BadRequest();

            // Директор может редактировать только свою компанию (название, заметки)
            if (CurrentCompanyId != 0 && CurrentCompanyId != id) return Forbid();

            _context.Entry(company).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCompany(int id)
        {
            if (CurrentCompanyId != 0) return Forbid("Только Разработчик может удалять компании.");

            var company = await _context.Companies.FindAsync(id);
            if (company == null) return NotFound();

            _context.Companies.Remove(company);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}