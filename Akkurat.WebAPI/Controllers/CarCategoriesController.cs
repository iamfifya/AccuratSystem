using Accurat.WebAPI.Data;
using AccuratSystem.Contracts.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accurat.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CarCategoriesController : ControllerBase
    {
        private readonly AppDbContext _context;
        public CarCategoriesController(AppDbContext context) => _context = context;

        // Читаем ID компании из заголовка (SaaS защита)
        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id)
            ? int.Parse(id)
            : 1;

        [HttpGet("by-branch/{branchId}")]
        public async Task<ActionResult<IEnumerable<CarCategory>>> GetByBranch(int branchId)
        {
            var branch = await _context.Branches.FindAsync(branchId);
            if (branch == null) return NotFound("Филиал не найден");

            return await _context.CarCategories
                .Where(c => c.CompanyId == branch.CompanyId)
                .OrderBy(c => c.SortOrder)
                .ToListAsync();
        }

        // 1. СОЗДАНИЕ КАТЕГОРИИ
        [HttpPost]
        public async Task<ActionResult<CarCategory>> CreateCategory(CarCategory category)
        {
            // Привязываем к компании из заголовка
            category.CompanyId = CurrentCompanyId;

            // Автоматически считаем следующий SortOrder, чтобы не задавать его вручную
            var maxOrder = await _context.CarCategories
                .Where(c => c.CompanyId == CurrentCompanyId)
                .Select(c => (int?)c.SortOrder)
                .MaxAsync() ?? 0;

            category.SortOrder = maxOrder + 1;

            _context.CarCategories.Add(category);
            await _context.SaveChangesAsync();

            return Ok(category);
        }

        // 2. ОБНОВЛЕНИЕ КАТЕГОРИИ
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategory(int id, CarCategory category)
        {
            if (id != category.Id) return BadRequest("ID не совпадает");

            var existing = await _context.CarCategories.FindAsync(id);
            if (existing == null) return NotFound();

            // ЗАЩИТА: Проверяем, принадлежит ли категория этой компании
            if (existing.CompanyId != CurrentCompanyId) return Forbid();

            // Обновляем только разрешенные поля
            existing.Name = category.Name;
            existing.SortOrder = category.SortOrder;

            _context.Entry(existing).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // 3. УДАЛЕНИЕ КАТЕГОРИИ
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.CarCategories.FindAsync(id);
            if (category == null) return NotFound();

            // ЗАЩИТА: Проверяем владельца
            if (category.CompanyId != CurrentCompanyId) return Forbid();

            _context.CarCategories.Remove(category);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
