using Accurat.WebAPI.Data;
using AccuratSystem.Contracts.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accurat.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiscountRulesController : ControllerBase
    {
        private readonly AppDbContext _context;
        public DiscountRulesController(AppDbContext context) => _context = context;

        // Читаем заголовок тенанта (компании)
        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id)
            ? int.Parse(id)
            : 1;

        // 1. Получить все правила компании
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DiscountRule>>> GetRules()
        {
            return await _context.DiscountRules
                .Where(r => r.CompanyId == CurrentCompanyId)
                .ToListAsync();
        }

        // 2. Создать новое правило
        [HttpPost]
        public async Task<ActionResult<DiscountRule>> CreateRule(DiscountRule rule)
        {
            rule.CompanyId = CurrentCompanyId;
            _context.DiscountRules.Add(rule);
            await _context.SaveChangesAsync();
            return Ok(rule);
        }

        // 3. Обновить правило
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRule(int id, DiscountRule rule)
        {
            if (id != rule.Id) return BadRequest();

            var existingRule = await _context.DiscountRules.FindAsync(id);
            if (existingRule == null) return NotFound();

            // Защита: нельзя менять правило другой компании
            if (existingRule.CompanyId != CurrentCompanyId) return Forbid();

            rule.CompanyId = existingRule.CompanyId;
            _context.Entry(rule).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // 4. Удалить правило
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRule(int id)
        {
            var rule = await _context.DiscountRules.FindAsync(id);
            if (rule == null) return NotFound();
            if (rule.CompanyId != CurrentCompanyId) return Forbid();

            _context.DiscountRules.Remove(rule);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
