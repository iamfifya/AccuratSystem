using AccuratSystem.Contracts.Models;
using AccuratSystem.Contracts.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Accurat.WebAPI.Data;

namespace Accurat.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        // Читаем заголовок из WPF
        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id) 
            ? int.Parse(id) 
            : 1;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        {
            // ИЗОЛЯЦИЯ + РЕЖИМ БОГА: Разработчик (0) видит всех, остальные — только свой персонал
            return await _context.Users
                .Include(u => u.Role)
                .Where(u => CurrentCompanyId == 0 || u.CompanyId == CurrentCompanyId)
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<User>> CreateUser(User user)
        {
            user.CompanyId = CurrentCompanyId; // Привязываем новичка к компании
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return Ok(user);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, User user)
        {
            if (id != user.Id) return BadRequest(new { message = "ID в URL и в объекте не совпадают" });

            _context.Entry(user).State = EntityState.Modified;

            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Users.Any(u => u.Id == id)) return NotFound(new { message = "Сотрудник не найден в базе" });
                throw;
            }
            return NoContent(); 
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Login == request.Login && u.PasswordHash == request.Password);

            if (user == null) return Unauthorized(new { message = "Неверный логин или пароль" });

            int? userCompanyId = user.CompanyId;
            var availableBranches = new List<Branch>();

            // 👑 РЕЖИМ БОГА: Разработчик видит вообще всю систему
            if (user.RoleId == 0 || user.Role?.Name == "Разработчик")
            {
                availableBranches = await _context.Branches.ToListAsync();
            }
            // Обычный Директор / Управляющий (строго в рамках своей компании)
            else if (userCompanyId.HasValue && (user.RoleId == 1 || user.RoleId == 2))
            {
                availableBranches = await _context.Branches
                    .Where(b => b.CompanyId == userCompanyId.Value)
                    .ToListAsync();
            }
            // Линейный персонал (строго свой филиал)
            else
            {
                if (user.BranchId.HasValue)
                {
                    var myBranch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == user.BranchId.Value);
                    if (myBranch != null) availableBranches.Add(myBranch);
                }
            }

            TenantFeaturesDto features = new TenantFeaturesDto();
            if (availableBranches.Any())
            {
                int primaryBranchId = availableBranches.First().Id;
                var tenantFeature = await _context.TenantFeatures
                    .FirstOrDefaultAsync(f => f.BranchId == primaryBranchId);

                if (tenantFeature != null)
                {
                    features = new TenantFeaturesDto
                    {
                        IsUpsellEnabled = tenantFeature.IsUpsellEnabled,
                        IsStorageEnabled = tenantFeature.IsStorageEnabled,
                        IsCrmMarketingEnabled = tenantFeature.IsCrmMarketingEnabled,
                        IsTelegramBossEnabled = tenantFeature.IsTelegramBossEnabled
                    };
                }
            }

            return Ok(new LoginResponseDto
            {
                User = user,
                Features = features,
                AvailableBranches = availableBranches,
                Message = "Успешно"
            });
        }
    }
}