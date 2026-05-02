using Accurat.WebAPI.Data;
using Accurat.WebAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accurat.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TransactionsController(AppDbContext context)
        {
            _context = context;
        }

        // ПОЛУЧИТЬ транзакции конкретного филиала (отсортированные по убыванию даты)
        [HttpGet("branch/{branchId}")]
        public async Task<ActionResult<IEnumerable<Transaction>>> GetTransactionsByBranch(int branchId)
        {
            return await _context.Transactions
                .Where(t => t.BranchId == branchId)
                .OrderByDescending(t => t.DateTime)
                .ToListAsync();
        }

        // ДОБАВИТЬ транзакцию
        [HttpPost]
        public async Task<ActionResult<Transaction>> CreateTransaction(Transaction transaction)
        {
            // Очищаем навигационные свойства, чтобы EF Core не пытался создать новые сущности
            transaction.Branch = null;
            transaction.Shift = null;
            transaction.Employee = null;

            transaction.DateTime = DateTime.UtcNow; // Принудительно ставим серверное UTC-время

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            return Ok(transaction);
        }

        [HttpGet("shift/{shiftId}")]
        public async Task<ActionResult<IEnumerable<Transaction>>> GetTransactionsByShift(int shiftId)
        {
            return await _context.Transactions
                .Where(t => t.ShiftId == shiftId)
                .OrderByDescending(t => t.DateTime)
                .ToListAsync();
        }
    }
}