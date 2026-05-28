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
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        {
            return await _context.Users.ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<User>> CreateUser(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return Ok(user);
        }
        // Обновить данные сотрудника
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, User user)
        {
            if (id != user.Id)
            {
                return BadRequest(new { message = "ID в URL и в объекте не совпадают" });
            }

            // Помечаем объект как измененный для Entity Framework
            _context.Entry(user).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Users.Any(u => u.Id == id))
                {
                    return NotFound(new { message = "Сотрудник не найден в базе" });
                }
                throw;
            }

            return NoContent(); // Успешное выполнение без возврата тела
        }
        // 1. Теперь в запросе передаем и логин, и пароль, и ID выбранного филиала
        public class LoginRequest
        {
            public string Login { get; set; }
            public string Password { get; set; }
            public int BranchId { get; set; } // <-- ДОБАВЛЯЕМ ЭТО
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Login == request.Login && u.PasswordHash == request.Password);

            if (user == null) return Unauthorized(new { message = "Неверный логин или пароль" });

            // !!!  используем BranchId ИЗ ЗАПРОСА (выбранный в WPF), 
            // а не тот, что привязан к пользователю в базе
            TenantFeaturesDto features = new TenantFeaturesDto();

            var tenantFeature = await _context.TenantFeatures
                .FirstOrDefaultAsync(f => f.BranchId == request.BranchId); // Используем request.BranchId

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

            return Ok(new LoginResponseDto
            {
                User = user,
                Features = features
            });
        }

    }
}