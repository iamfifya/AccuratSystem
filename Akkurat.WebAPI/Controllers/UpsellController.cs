using Accurat.WebAPI.Data;
using AccuratSystem.Contracts.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Accurat.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UpsellController : ControllerBase
    {
        private readonly AppDbContext _context;
        public UpsellController(AppDbContext context) => _context = context;

        // ДОБАВЛЯЕМ ЧТЕНИЕ ЗАГОЛОВКА
        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id) ? int.Parse(id) : 1;

        // === 1. УМНАЯ ВЫДАЧА СОВЕТА (Для кассира) ===
        [HttpGet("suggest")]
        public async Task<ActionResult<UpsellSuggestion>> GetSuggestion(
            [FromQuery] List<int> currentServices,
            [FromQuery] int branchId) // branchId оставляем для совместимости с WPF, но не используем для фич
        {
            if (currentServices == null || !currentServices.Any())
                return NotFound();

            // Проверяем лицензию по компании из заголовка!
            var tenantFeature = await _context.TenantFeatures.FirstOrDefaultAsync(f => f.CompanyId == CurrentCompanyId);

            // Если Разработчик (CurrentCompanyId == 0), то модуль включен по умолчанию
            if (CurrentCompanyId != 0 && (tenantFeature == null || !tenantFeature.IsUpsellEnabled))
                return StatusCode(403, "Модуль 'Умный кассир' отключен.");

            // Ищем самое ВЫГОДНОЕ предложение (сортировка по бонусу)
            var suggestion = await _context.UpsellSuggestions
                .Where(s => currentServices.Contains(s.TriggerServiceId)
                         && !currentServices.Contains(s.SuggestedServiceId))
                .OrderByDescending(s => s.BonusAmount)
                .FirstOrDefaultAsync();

            if (suggestion == null) return NotFound();

            return Ok(suggestion);
        }

        // === 2. ПОЛУЧИТЬ ВСЕ ПРАВИЛА (Для директора) ===
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UpsellSuggestion>>> GetAllRules()
        {
            return await _context.UpsellSuggestions.ToListAsync();
        }

        // === 3. ДОБАВИТЬ ПРАВИЛО (Для директора) ===
        [HttpPost]
        public async Task<ActionResult<UpsellSuggestion>> CreateRule(UpsellSuggestion rule)
        {
            _context.UpsellSuggestions.Add(rule);
            await _context.SaveChangesAsync();
            return Ok(rule);
        }

        // === 4. УДАЛИТЬ ПРАВИЛО (Для директора) ===
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRule(int id)
        {
            var rule = await _context.UpsellSuggestions.FindAsync(id);
            if (rule == null) return NotFound();

            _context.UpsellSuggestions.Remove(rule);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}