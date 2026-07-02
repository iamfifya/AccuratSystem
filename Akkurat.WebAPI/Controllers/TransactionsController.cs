using AccuratSystem.Contracts.Models;
using AccuratSystem.Contracts.Enums;
using AccuratSystem.Contracts.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Accurat.WebAPI.Data;

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

        // Чтение ID компании из заголовка для изоляции SaaS
        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id)
            ? int.Parse(id)
            : 1;

        #region Хелперы безопасности

        // Проверяет, принадлежит ли филиал текущей компании
        private async Task<bool> VerifyBranchAccess(int branchId)
        {
            if (CurrentCompanyId == 0) return true; // Режим разработчика
            var branch = await _context.Branches.FindAsync(branchId);
            return branch != null && branch.CompanyId == CurrentCompanyId;
        }

        // Проверяет, принадлежит ли смена текущей компании
        private async Task<bool> VerifyShiftAccess(int shiftId)
        {
            if (CurrentCompanyId == 0) return true; // Режим разработчика
            var shift = await _context.Shifts
                .Include(s => s.Branch)
                .FirstOrDefaultAsync(s => s.Id == shiftId);

            return shift != null && shift.Branch != null && shift.Branch.CompanyId == CurrentCompanyId;
        }
        #endregion

        // ПОЛУЧИТЬ транзакции конкретного филиала
        [HttpGet("branch/{branchId}")]
        public async Task<ActionResult<IEnumerable<Transaction>>> GetTransactionsByBranch(int branchId)
        {
            // БЕЗОПАСНОСТЬ: Проверяем доступ к филиалу
            if (!await VerifyBranchAccess(branchId))
            {
                return (CurrentCompanyId != 0) ? Forbid("Доступ к транзакциям этого филиала запрещен.") : NotFound();
            }

            return await _context.Transactions
                .Where(t => t.BranchId == branchId)
                .OrderByDescending(t => t.DateTime)
                .ToListAsync();
        }

        // ДОБАВИТЬ транзакцию
        [HttpPost]
        public async Task<ActionResult<Transaction>> CreateTransaction(Transaction transaction)
        {
            // БЕЗОПАСНОСТЬ: Проверяем, может ли пользователь создавать записи в этом филиале
            if (!await VerifyBranchAccess(transaction.BranchId))
            {
                return (CurrentCompanyId != 0) ? Forbid("Вы не имеете прав создавать транзакции в этом филиале.") : BadRequest("Филиал не найден.");
            }

            transaction.DateTime = DateTime.UtcNow; // Принудительно ставим серверное UTC-время

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            return Ok(transaction);
        }

        [HttpGet("shift/{shiftId}")]
        public async Task<ActionResult<IEnumerable<Transaction>>> GetTransactionsByShift(int shiftId)
        {
            // БЕЗОПАСНОСТЬ: Проверяем доступ к смене
            if (!await VerifyShiftAccess(shiftId))
            {
                return (CurrentCompanyId != 0) ? Forbid("Доступ к транзакциям этой смены запрещен.") : NotFound();
            }

            return await _context.Transactions
                .Where(t => t.ShiftId == shiftId)
                .OrderByDescending(t => t.DateTime)
                .ToListAsync();
        }
    }
}
