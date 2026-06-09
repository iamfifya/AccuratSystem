using Accurat.WebAPI.Data;
using AccuratSystem.Contracts.DTOs;
using AccuratSystem.Contracts.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.Intrinsics.X86;

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
            user.CompanyId = CurrentCompanyId;

            // --- Хэшируем пароль перед сохранением ---
            // Если пароль пришел, мы превращаем его в секретный хэш
            if (!string.IsNullOrEmpty(user.PasswordHash))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
            }
            // ------------------------------------------------------

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return Ok(user);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, User user)
        {
            if (id != user.Id) return BadRequest(new { message = "ID в URL и в объекте не совпадают" });

            // 1. Достаем "оригинал" пользователя из базы без отслеживания (AsNoTracking)
            var existingUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            if (existingUser == null) return NotFound(new { message = "Сотрудник не найден в базе" });

            // 2. ЗАЩИТА SAAS: Жестко восстанавливаем CompanyId из базы! 
            // Клиент не имеет права менять привязку к компании.
            user.CompanyId = existingUser.CompanyId;

            // 3. ЗАЩИТА ПАРОЛЯ: Если WPF прислал пустой пароль (не меняли в окне), 
            // возвращаем старый хеш из базы, чтобы не затереть его.
            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                user.PasswordHash = existingUser.PasswordHash;
            }

            // 4. Теперь можно безопасно сохранять
            _context.Entry(user).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw;
            }

            return NoContent();
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
        {
            // 1. Ищем пользователя по логину
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Login == request.Login);

            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
                return Unauthorized(new { message = "Неверный логин или пароль" });

            bool isPasswordCorrect = false;

            try
            {
                // Пробуем проверить как хэш
                isPasswordCorrect = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                // ЕСЛИ МЫ ТУТ: значит в базе лежит старый пароль (просто текст)
                // Проверяем его обычным сравнением
                if (user.PasswordHash == request.Password)
                {
                    isPasswordCorrect = true;

                    // АВТО-ОБНОВЛЕНИЕ: Раз пароль подошел, давайте его захешируем прямо сейчас, чтобы в следующий раз не было ошибки
                   
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();
                }
            }

            if (!isPasswordCorrect)
                return Unauthorized(new { message = "Неверный логин или пароль" });

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

            // Берем CompanyId у пользователя. 
            // Если это разработчик (CompanyId == null/0), даем ему фичи первой компании (или заглушку)
            int targetCompanyId = user.CompanyId ?? 1;

            var tenantFeature = await _context.TenantFeatures
                .FirstOrDefaultAsync(f => f.CompanyId == targetCompanyId);

            if (tenantFeature != null)
            {
                features = new TenantFeaturesDto
                {
                    IsUpsellEnabled = tenantFeature.IsUpsellEnabled,
                    IsServicesEnabled = tenantFeature.IsServicesEnabled,
                    IsCrmMarketingEnabled = tenantFeature.IsCrmMarketingEnabled,
                    IsTelegramBossEnabled = tenantFeature.IsTelegramBossEnabled
                };
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