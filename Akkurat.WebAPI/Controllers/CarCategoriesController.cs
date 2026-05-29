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

        [HttpGet("by-branch/{branchId}")]
        public async Task<ActionResult<IEnumerable<CarCategory>>> GetByBranch(int branchId)
        {
            // 1. Находим филиал, чтобы узнать его CompanyId
            var branch = await _context.Branches.FindAsync(branchId);
            if (branch == null) return NotFound("Филиал не найден");

            // 2. Отдаем категории, принадлежащие именно этой компании, отсортированные по порядку
            var categories = await _context.CarCategories
                .Where(c => c.CompanyId == branch.CompanyId)
                .OrderBy(c => c.SortOrder)
                .ToListAsync();

            return Ok(categories);
        }
    }
}