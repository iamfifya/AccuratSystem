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
            // Если пользователь — обычный сотрудник/директор (не 0), 
            // мы ЖЕСТКО привязываем филиал к его компании.
            if (CurrentCompanyId != 0)
            {
                branch.CompanyId = CurrentCompanyId;
            }
            // Если же CurrentCompanyId == 0 (Разработчик), 
            // мы НЕ ПЕРЕЗАПИСЫВАЕМ branch.CompanyId, 
            // позволяя сохранить тот ID, который был выбран в WPF.

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
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBranch(int id, Branch branch)
        {
            if (id != branch.Id) return BadRequest(new { message = "ID в URL и объекте не совпадают" });

            // Защита: редактировать чужие филиалы может только Разработчик
            if (CurrentCompanyId != 0 && branch.CompanyId != CurrentCompanyId)
                return Forbid();

            _context.Entry(branch).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Branches.Any(b => b.Id == id)) return NotFound();
                throw;
            }

            return NoContent();
        }
    }
}