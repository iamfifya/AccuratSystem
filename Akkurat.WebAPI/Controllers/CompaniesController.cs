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

            // 1. Создаем саму компанию
            company.RegistrationDate = DateTime.UtcNow;
            _context.Companies.Add(company);
            await _context.SaveChangesAsync(); // Сохраняем, чтобы получить company.Id

            try
            {
                // --- МАГИЯ АВТОЗАПОЛНЕНИЯ (СЕЙФ-ПАКЕТ) ---

                // 2. Создаем базовые настройки компании
                var settings = new CompanySettings
                {
                    CompanyId = company.Id,
                    CompanySharePercentage = 65m,
                    DefaultAppointmentDuration = 60
                };
                _context.CompanySettings.Add(settings);

                // 3. Копируем СПОСОБЫ ОПЛАТЫ из шаблона (Компания №1)
                var defaultPayments = await _context.PaymentMethods
                    .Where(p => p.CompanyId == 1).ToListAsync();
                foreach (var p in defaultPayments)
                {
                    _context.PaymentMethods.Add(new PaymentMethod
                    {
                        Name = p.Name,
                        IsActive = p.IsActive,
                        SortOrder = p.SortOrder,
                        CompanyId = company.Id
                    });
                }

                // 4. Копируем СТАТУСЫ из шаблона (Компания №1)
                var defaultStatuses = await _context.OrderStatuses
                    .Where(s => s.CompanyId == 1).ToListAsync();
                foreach (var s in defaultStatuses)
                {
                    _context.OrderStatuses.Add(new OrderStatus
                    {
                        Name = s.Name,
                        Icon = s.Icon,
                        SortOrder = s.SortOrder,
                        ColorHex = s.ColorHex,
                        CompanyId = company.Id
                    });
                }

                // 5. Копируем КАТЕГОРИИ АВТО из шаблона (Компания №1)
                // Это критично для работы цен в прайсе!
                var defaultCategories = await _context.CarCategories
                    .Where(c => c.CompanyId == 1).ToListAsync();
                foreach (var c in defaultCategories)
                {
                    _context.CarCategories.Add(new CarCategory
                    {
                        Name = c.Name,
                        SortOrder = c.SortOrder,
                        CompanyId = company.Id
                    });
                }

                // Сохраняем всё одним махом
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Если произошла ошибка при заполнении справочников, 
                // компания всё равно создана, но мы сообщаем об ошибке.
                return StatusCode(500, $"Компания создана, но базовые справочники не заполнены: {ex.Message}");
            }

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