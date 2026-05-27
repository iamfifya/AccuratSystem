using Accurat.WebAPI.Data;
using AccuratSystem.Contracts.DTOs;
using Akkurat.WebAPI.Models;
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

        // === 1. УМНАЯ ВЫДАЧА СОВЕТА (Для кассира) ===
        [HttpGet("suggest")]
        public async Task<ActionResult<UpsellSuggestion>> GetSuggestion(
            [FromQuery] List<int> currentServices,
            [FromQuery] int branchId)
        {
            if (currentServices == null || !currentServices.Any())
                return NotFound();

            // Проверяем, куплен ли модуль для этого филиала
            var tenantFeature = await _context.TenantFeatures.FirstOrDefaultAsync(f => f.BranchId == branchId);
            if (tenantFeature == null || !tenantFeature.IsUpsellEnabled)
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