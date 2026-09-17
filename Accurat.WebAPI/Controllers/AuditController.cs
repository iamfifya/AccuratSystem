using Accurat.WebAPI.Data;
using AccuratSystem.Contracts.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Accurat.WebAPI.Time;

namespace Accurat.WebAPI.Controllers
{
    /// <summary>
    /// Единый журнал действий: события заказов + события смен.
    /// URL: /api/audit-log (явный путь, чтобы совпадал с клиентом).
    /// </summary>
    [Route("api/audit-log")]
    [ApiController]
    public class AuditController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuditController(AppDbContext context) => _context = context;

        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id)
            ? int.Parse(id)
            : 1;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AuditEntryDto>>> GetAuditLog(
            [FromQuery] int? userId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string entryType = null,
            [FromQuery] int? branchId = null,
            [FromQuery] int pageSize = 500,
            [FromQuery] int pageNumber = 1)
        {
            // === События заказов ===
            var orderEventsQuery = _context.OrderTimelineEntries.AsQueryable();

            // === События смен ===
            var shiftEventsQuery = _context.ShiftTimelineEntries.AsQueryable();

            // Изоляция тенанта: только свои филиалы
            if (CurrentCompanyId != 0)
            {
                var myBranchIds = _context.Branches
                    .Where(b => b.CompanyId == CurrentCompanyId)
                    .Select(b => b.Id);

                orderEventsQuery = orderEventsQuery
                    .Join(_context.Orders,
                        e => e.OrderId,
                        o => o.Id,
                        (e, o) => new { Entry = e, BranchId = o.BranchId })
                    .Where(x => myBranchIds.Contains(x.BranchId))
                    .Select(x => x.Entry);

                shiftEventsQuery = shiftEventsQuery
                    .Join(_context.Shifts,
                        e => e.ShiftId,
                        s => s.Id,
                        (e, s) => new { Entry = e, BranchId = s.BranchId })
                    .Where(x => myBranchIds.Contains(x.BranchId))
                    .Select(x => x.Entry);
            }

            // Преобразуем события заказов в общий формат
            var orderEvents = orderEventsQuery
                .Join(_context.Orders,
                    e => e.OrderId,
                    o => o.Id,
                    (e, o) => new AuditEntryDto
                    {
                        Timestamp = e.Timestamp,
                        EventType = e.EntryType.ToString(),
                        Message = e.Message,
                        CreatedBy = e.CreatedBy,
                        RelatedEntityId = e.RelatedEntityId,
                        OrderId = e.OrderId,
                        ShiftId = null,
                        BranchId = o.BranchId
                    });

            // Преобразуем события смен в общий формат
            var shiftEvents = shiftEventsQuery
                .Join(_context.Shifts,
                    e => e.ShiftId,
                    s => s.Id,
                    (e, s) => new AuditEntryDto
                    {
                        Timestamp = e.Timestamp,
                        EventType = e.EventType,
                        Message = e.Message,
                        CreatedBy = e.CreatedBy,
                        RelatedEntityId = e.RelatedEntityId,
                        OrderId = null,
                        ShiftId = e.ShiftId,
                        BranchId = s.BranchId
                    });

            // Объединяем обе ленты
            var combined = orderEvents.Union(shiftEvents);

            // Фильтры
            if (userId.HasValue)
                combined = combined.Where(e => e.RelatedEntityId == userId.Value);

            if (startDate.HasValue)
            {
                var startUtc = BusinessTime.ToInstantUtc(startDate.Value);
                combined = combined.Where(e => e.Timestamp >= startUtc);
            }

            if (endDate.HasValue)
            {
                var endUtc = BusinessTime.ToInstantUtc(endDate.Value.Date.AddDays(1).AddTicks(-1));
                combined = combined.Where(e => e.Timestamp <= endUtc);
            }

            if (!string.IsNullOrWhiteSpace(entryType))
                combined = combined.Where(e => e.EventType == entryType);

            if (branchId.HasValue)
                combined = combined.Where(e => e.BranchId == branchId.Value);

            var result = await combined
                .OrderByDescending(e => e.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(result);
        }
    }
}