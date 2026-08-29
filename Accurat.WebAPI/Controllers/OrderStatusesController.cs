using Accurat.WebAPI.Data;
using AccuratSystem.Contracts.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accurat.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderStatusesController : ControllerBase
    {
        private readonly AppDbContext _context;
        public OrderStatusesController(AppDbContext context) => _context = context;

        [HttpGet("by-branch/{branchId}")]
        public async Task<ActionResult<IEnumerable<OrderStatus>>> GetByBranch(int branchId)
        {
            var branch = await _context.Branches.FindAsync(branchId);
            if (branch == null) return NotFound("Филиал не найден");

            var statuses = await _context.OrderStatuses
                .Where(s => s.CompanyId == branch.CompanyId)
                .OrderBy(s => s.SortOrder)
                .ToListAsync();

            return Ok(statuses);
        }
    }
}