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

        // Чтение ID компании из заголовка (SaaS защита)
        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id)
            ? int.Parse(id)
            : 1;

        // Хелпер для проверки доступа к филиалу
        private async Task<bool> VerifyBranchAccess(int branchId)
        {
            if (CurrentCompanyId == 0) return true; // Режим разработчика
            var branch = await _context.Branches.FindAsync(branchId);
            return branch != null && branch.CompanyId == CurrentCompanyId;
        }

        [HttpGet("by-branch/{branchId}")]
        public async Task<ActionResult<CompanySettings>> GetByBranch(int branchId)
        {
            // БЕЗОПАСНОСТЬ: Проверяем, имеет ли пользователь доступ к этому филиалу
            if (!await VerifyBranchAccess(branchId))
            {
                return (CurrentCompanyId != 0) ? Forbid("Доступ к настройкам этой компании запрещен.") : NotFound("Филиал не найден.");
            }

            // Теперь мы уверены, что филиал принадлежит текущему тенанту
            var branch = await _context.Branches.FindAsync(branchId);
            var settings = await _context.CompanySettings.FindAsync(branch.CompanyId);

            // Если настроек вдруг нет, возвращаем дефолтные (создаем объект в памяти)
            return Ok(settings ?? new CompanySettings { CompanyId = branch.CompanyId });
        }
    }
}
