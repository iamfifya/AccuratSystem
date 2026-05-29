using Accurat.WebAPI.Data;
using AccuratSystem.Contracts.Models;
using Microsoft.AspNetCore.Mvc;

namespace Accurat.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CompanySettingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public CompanySettingsController(AppDbContext context) => _context = context;

        [HttpGet("by-branch/{branchId}")]
        public async Task<ActionResult<CompanySettings>> GetByBranch(int branchId)
        {
            var branch = await _context.Branches.FindAsync(branchId);
            if (branch == null) return NotFound("Филиал не найден");

            var settings = await _context.CompanySettings.FindAsync(branch.CompanyId);

            // Если настроек вдруг нет, возвращаем дефолтные
            return Ok(settings ?? new CompanySettings { CompanyId = branch.CompanyId });
        }
    }
}