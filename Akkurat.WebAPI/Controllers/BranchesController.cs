using AccuratSystem.Contracts.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Accurat.WebAPI.Data;

namespace Accurat.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BranchesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BranchesController(AppDbContext context)
        {
            _context = context;
        }

        // Читаем заголовок из WPF
        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id)
            ? int.Parse(id)
            : 1;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Branch>>> GetBranches()
        {
            // Отдаем всё, если ID компании = 0 (Разработчик)
            return await _context.Branches
                .Where(b => CurrentCompanyId == 0 || b.CompanyId == CurrentCompanyId)
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Branch>> CreateBranch(Branch branch)
        {
            // Принудительно привязываем новый филиал к текущей компании!
            branch.CompanyId = CurrentCompanyId;

            _context.Branches.Add(branch);
            await _context.SaveChangesAsync();
            return Ok(branch);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBranch(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null || branch.CompanyId != CurrentCompanyId) return NotFound();

            _context.Branches.Remove(branch);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}