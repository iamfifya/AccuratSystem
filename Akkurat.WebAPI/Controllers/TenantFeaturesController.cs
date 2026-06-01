using Accurat.WebAPI.Data;
using AccuratSystem.Contracts.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accurat.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TenantFeaturesController : ControllerBase
    {
        private readonly AppDbContext _context;
        public TenantFeaturesController(AppDbContext context) => _context = context;

        // Читаем заголовок
        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id) ? int.Parse(id) : 1;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TenantFeature>>> GetFeatures()
        {
            // Возвращаем все лицензии (если Разработчик)
            if (CurrentCompanyId == 0)
                return await _context.TenantFeatures.ToListAsync();

            // Теперь .Include(f => f.Company) сработает без ошибок!
            return await _context.TenantFeatures
                .Include(f => f.Company)
                .Where(f => f.Company != null && f.Company.Id == CurrentCompanyId)
                .ToListAsync();
        }

        [HttpPut("{companyId}")]
        public async Task<IActionResult> UpdateFeature(int companyId, TenantFeature feature)
        {
            if (companyId != feature.CompanyId) return BadRequest();
            if (CurrentCompanyId != 0) return Forbid("Управлять лицензиями DLC может только Разработчик.");

            _context.Entry(feature).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}