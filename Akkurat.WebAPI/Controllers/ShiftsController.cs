using Accurat.WebAPI.Data;
using Accurat.WebAPI.Models;
using Akkurat.WebAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accurat.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShiftsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ShiftsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Shift>>> GetShifts()
        {
            return await _context.Shifts.ToListAsync();
        }

        // Открыть смену
        [HttpPost]
        public async Task<ActionResult<Shift>> OpenShift(Shift shift)
        {
            // Убираем объект Branch, чтобы EF не пытался создать новый филиал
            shift.Branch = null;

            // Приводим дату к UTC без времени для точного поиска
            DateTime targetDate = DateTime.SpecifyKind(shift.Date.Date, DateTimeKind.Utc);

            // Ищем уже существующую смену на этот день и филиал
            Shift existingShift = await _context.Shifts
                .FirstOrDefaultAsync(s => s.BranchId == shift.BranchId && s.Date == targetDate);

            if (existingShift != null)
            {
                // Смена уже была — ВОЗОБНОВЛЯЕМ ЕЁ
                existingShift.IsClosed = false;

                // Обновляем список сотрудников на случай, если вышли другие люди
                existingShift.EmployeeIds = shift.EmployeeIds;

                // Обязательно затираем время закрытия, так как смена снова в работе
                existingShift.EndTime = null;

                _context.Shifts.Update(existingShift);
                await _context.SaveChangesAsync();

                return Ok(existingShift);
            }
            else
            {
                // Смены сегодня еще не было — СОЗДАЕМ НОВУЮ
                shift.StartTime = DateTime.UtcNow;
                shift.Date = targetDate;
                shift.IsClosed = false;

                _context.Shifts.Add(shift);
                await _context.SaveChangesAsync();

                return Ok(shift);
            }
        }

        // Закрыть смену
        [HttpPatch("{id}/close")]
        public async Task<IActionResult> CloseShift(int id)
        {
            var shift = await _context.Shifts.FindAsync(id);
            if (shift == null) return NotFound();

            shift.EndTime = DateTime.UtcNow;
            shift.IsClosed = true;
            await _context.SaveChangesAsync();

            return Ok(shift);
        }

        [HttpGet("{id}/cashbox")]
        public async Task<ActionResult<CashboxSummary>> GetCashboxSummary(int id)
        {
            var shift = await _context.Shifts.FindAsync(id);
            if (shift == null) return NotFound();

            // 1. Грузим всё необходимое из базы
            var orders = await _context.Orders
                .Where(o => o.ShiftId == id && o.Status == "Выполнен" && o.PaymentMethod == "Наличные")
                .ToListAsync();

            var transactions = await _context.Transactions
                .Where(t => t.ShiftId == id)
                .ToListAsync();

            var allServices = await _context.Services.ToListAsync();

            // 2. Считаем выручку
            decimal cashRevenue = orders.Sum(o => o.FinalPrice);

            // 3. Считаем движения по кассе
            decimal deposits = transactions.Where(t => t.Type == "Приход" || t.Type == "Размен").Sum(t => t.Amount);
            decimal advances = transactions.Where(t => t.Type == "Аванс мойщику").Sum(t => t.Amount);
            decimal expenses = transactions.Where(t => t.Type == "Расход").Sum(t => t.Amount);
            decimal withdrawals = transactions.Where(t => t.Type == "Инкассация").Sum(t => t.Amount);

            // 4. Считаем доплату до минималки
            decimal totalTopUp = 0;
            var groupedByWasher = orders.GroupBy(o => o.WasherId);
            foreach (var group in groupedByWasher)
            {
                // Базовая ЗП мойщика (35%)
                decimal basePay = 0;
                foreach (var order in group)
                {
                    decimal servicesTotal = (order.ServiceIds ?? new List<int>())
                        .Sum(sid =>
                        {
                            var svc = allServices.FirstOrDefault(s => s.Id == sid);

                            // Если услуга найдена, у неё есть прайс-лист и в нём есть цена для кузова этого заказа:
                            if (svc != null && svc.PriceByBodyType != null && svc.PriceByBodyType.TryGetValue(order.BodyTypeCategory, out decimal price))
                            {
                                return price;
                            }
                            return 0m; // Если цены нет, возвращаем 0
                        });

                    decimal baseAmount = servicesTotal + order.ExtraCost;
                    basePay += baseAmount * 0.35m; // WASHER_PERCENT
                }

                // Если не заработал 1000, докидываем
                // if (basePay > 0 && basePay < 1000m)
                // {
                //     totalTopUp += (1000m - basePay);
                // }
            }

            // 5. Итоговые цифры
            return new CashboxSummary
            {
                CashInHand = cashRevenue + deposits - (advances + expenses + withdrawals),
                TotalExpenses = expenses + advances,
                NetCashProfit = (cashRevenue * 0.65m) - expenses - totalTopUp
            };
        }
    }
}