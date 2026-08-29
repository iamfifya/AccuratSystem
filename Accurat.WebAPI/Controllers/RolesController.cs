using Accurat.WebAPI.Data;
using AccuratSystem.Contracts.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accurat.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RolesController : ControllerBase
    {
        private readonly AppDbContext _context;
        public RolesController(AppDbContext context) => _context = context;

        // Чтение ID компании из заголовка (SaaS защита)
        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id)
            ? int.Parse(id)
            : 1;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Role>>> GetRoles()
        {
            // Чтение ролей разрешено всем, так как они используются в интерфейсах выбора сотрудника
            return await _context.Roles.ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Role>> CreateRole(Role role)
        {
            // БЕЗОПАСНОСТЬ: Только Разработчик (CompanyId = 0) может создавать новые глобальные роли
            if (CurrentCompanyId != 0)
                return Forbid("Создание новых ролей разрешено только главному администратору системы.");

            _context.Roles.Add(role);
            await _context.SaveChangesAsync();
            return Ok(role);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRole(int id, Role role)
        {
            if (id != role.Id) return BadRequest("ID в URL и в объекте не совпадают");

            // БЕЗОПАСНОСТЬ: Только Разработчик может изменять названия ролей
            if (CurrentCompanyId != 0)
                return Forbid("Редактирование ролей разрешено только главному администратору системы.");

            var existingRole = await _context.Roles.FindAsync(id);
            if (existingRole == null) return NotFound("Роль не найдена");

            _context.Entry(role).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRole(int id)
        {
            // БЕЗОПАСНОСТЬ: Только Разработчик может удалять роли
            if (CurrentCompanyId != 0)
                return Forbid("Удаление ролей разрешено только главному администратору системы.");

            var role = await _context.Roles.FindAsync(id);
            if (role == null) return NotFound("Роль не найдена");

            _context.Roles.Remove(role);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
