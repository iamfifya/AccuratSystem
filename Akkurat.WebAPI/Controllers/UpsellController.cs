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

        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id) ? int.Parse(id) : 1;

        // 1. УМНАЯ ВЫДАЧА СОВЕТА
        [HttpGet("suggest")]
        public async Task<ActionResult<UpsellSuggestion>> GetSuggestion([FromQuery] List<int> currentServices, [FromQuery] int branchId)
        {
            if (currentServices == null || !currentServices.Any()) return NotFound();

            // Проверяем лицензию модуля для ТЕКУЩЕЙ компании
            var tenantFeature = await _context.TenantFeatures.FirstOrDefaultAsync(f => f.CompanyId == CurrentCompanyId);
            if (CurrentCompanyId != 0 && (tenantFeature == null || !tenantFeature.IsUpsellEnabled))
                return StatusCode(403, "Модуль 'Умный кассир' отключен.");

            // Ищем правило ТОЛЬКО для текущей компании
            var suggestion = await _context.UpsellSuggestions
                .Where(s => s.CompanyId == CurrentCompanyId) // ДОБАВЛЕНО: Фильтр по компании
                .Where(s => currentServices.Contains(s.TriggerServiceId) && !currentServices.Contains(s.SuggestedServiceId))
                .OrderByDescending(s => s.BonusAmount)
                .FirstOrDefaultAsync();

            if (suggestion == null) return NotFound();
            return Ok(suggestion);
        }

        // 2. ПОЛУЧИТЬ ВСЕ ПРАВИЛА (только свои)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UpsellSuggestion>>> GetAllRules()
        {
            // Возвращаем только правила текущей компании
            return await _context.UpsellSuggestions
                .Where(s => s.CompanyId == CurrentCompanyId) // ДОБАВЛЕНО: Фильтр по компании
                .ToListAsync();
        }

        // 3. ДОБАВИТЬ ПРАВИЛО
        [HttpPost]
        public async Task<ActionResult<UpsellSuggestion>> CreateRule(UpsellSuggestion rule)
        {
            // Жестко привязываем правило к компании того, кто его создает
            rule.CompanyId = CurrentCompanyId;

            _context.UpsellSuggestions.Add(rule);
            await _context.SaveChangesAsync();
            return Ok(rule);
        }

        // 4. УДАЛИТЬ ПРАВИЛО
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRule(int id)
        {
            var rule = await _context.UpsellSuggestions.FindAsync(id);
            if (rule == null) return NotFound();

            // ЗАЩИТА: Запрещаем удалять чужие правила
            if (rule.CompanyId != CurrentCompanyId) return Forbid();

            _context.UpsellSuggestions.Remove(rule);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}