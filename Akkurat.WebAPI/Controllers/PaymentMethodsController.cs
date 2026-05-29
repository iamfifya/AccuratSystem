using Accurat.WebAPI.Data;
using AccuratSystem.Contracts.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accurat.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentMethodsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public PaymentMethodsController(AppDbContext context) => _context = context;

        [HttpGet("by-branch/{branchId}")]
        public async Task<ActionResult<IEnumerable<PaymentMethod>>> GetByBranch(int branchId)
        {
            var branch = await _context.Branches.FindAsync(branchId);
            if (branch == null) return NotFound("Филиал не найден");

            var methods = await _context.PaymentMethods
                .Where(p => p.CompanyId == branch.CompanyId && p.IsActive)
                .OrderBy(p => p.SortOrder)
                .ToListAsync();

            return Ok(methods);
        }
    }
}