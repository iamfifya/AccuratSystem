using AccuratSystem.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using AccuratPanelCarWashing.Services;

namespace AccuratPanelCarWashing.Services
{
    /// <summary>
    /// ЕДИНСТВЕННЫЙ источник истины для расчётов заказов и зарплат.
    /// </summary>
    public static class OrderMath
    {
        public const decimal WASHER_PERCENT = 0.35m;

        /// <summary>
        /// Вытаскивает бонус за апселл ("Умный кассир") из текстовых заметок заказа.
        /// </summary>
        public static decimal ExtractUpsellBonus(string notes)
        {
            if (string.IsNullOrEmpty(notes) || !notes.Contains("бонус: +"))
                return 0;

            try
            {
                int startIndex = notes.IndexOf("бонус: +") + "бонус: +".Length;
                int endIndex = notes.IndexOf(" ₽", startIndex);

                if (endIndex > startIndex)
                {
                    string bonusStr = notes.Substring(startIndex, endIndex - startIndex).Trim();
                    if (decimal.TryParse(bonusStr, out decimal bonus))
                    {
                        return bonus;
                    }
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Рассчитывает ВСЕ значения для одного заказа.
        /// </summary>
        public static OrderCalculation Calculate(Order order, List<Service> allServices, List<User> washers = null)
        {
            var calc = new OrderCalculation();
            if (order == null) return calc;

            var washer = washers?.FirstOrDefault(w => w.Id == order.GetWasherId());
            decimal basePercentage = washer?.BaseWagePercentage ?? 35m;

            decimal servicesTotal = 0;
            decimal washerEarnings = 0;

            if (order.ServiceIds != null && allServices != null)
            {
                foreach (var sid in order.ServiceIds)
                {
                    var svc = allServices.FirstOrDefault(s => s.Id == sid);
                    if (svc != null)
                    {
                        decimal price = svc.PriceByBodyType.TryGetValue(order.BodyTypeCategory, out var p)
                            ? p
                            : (svc.PriceByBodyType.TryGetValue(1, out var def) ? def : 0);

                        servicesTotal += price;

                        decimal activePercentage = svc.CustomWagePercentage ?? basePercentage;
                        washerEarnings += price * (activePercentage / 100m);
                    }
                }
            }

            calc.ServicesTotal = servicesTotal;

            decimal baseAmount = servicesTotal + order.ExtraCost;
            if (order.ExtraCost > 0)
            {
                washerEarnings += order.ExtraCost * (basePercentage / 100m);
            }

            decimal actualDiscount = 0;
            if (order.DiscountPercent > 0)
            {
                actualDiscount = baseAmount * (order.DiscountPercent / 100m);
            }
            else if (order.DiscountAmount > 0)
            {
                actualDiscount = order.DiscountAmount;
            }

            calc.FinalPrice = baseAmount - actualDiscount;

            // ВНЕДРЯЕМ АПСЕЛЛ: Вытаскиваем бонус из строки
            calc.UpsellBonus = ExtractUpsellBonus(order.Notes);

            calc.WasherEarnings = washerEarnings;
            // Доход компании уменьшается на ЗП мойщика и на бонус апселла кассиру
            calc.CompanyEarnings = calc.FinalPrice - calc.WasherEarnings - calc.UpsellBonus;

            return calc;
        }

        /// <summary>
        /// Формирует полный расчет зарплаты сотрудника за смену (с окладом, процентами и авансами).
        /// </summary>
        public static EmployeeShiftStats CalculateShiftStats(
                      IEnumerable<Order> branchOrdersForShift,
                      List<Service> allServices,
                      User currentEmployee, // Тот, для кого считаем смену (мойщик или админ)
                      List<User> allUsers = null,
                      decimal advancesTaken = 0m)
        {
            var stats = new EmployeeShiftStats();
            if (currentEmployee == null) return stats;

            stats.AdvancesTotal = advancesTaken;

            if (currentEmployee.RoleId == 3) // 🛠️ МOЙЩИК: Считаем сделку по его заказам
            {
                var myOrders = branchOrdersForShift.Where(o => o.GetWasherId() == currentEmployee.Id).ToList();
                stats.PieceworkEarnings = myOrders.Sum(o => Calculate(o, allServices, allUsers).WasherEarnings);
                stats.FixedSalary = 0m;
                stats.UpsellBonusTotal = 0m;
            }
            else // 👑 АДМИНИСТРАТОР / ДИРЕКТОР: Берем оклад + % от кассы + бонусы кассира
            {
                // Оклад за выход из карточки
                stats.FixedSalary = currentEmployee.BaseSalaryPerShift;

                // Процент от общей выручки филиала за смену (если установлен в карточке)
                var completedOrders = branchOrdersForShift.Where(o => o.Status == "Выполнен" || o.Status == "Завершен").ToList();
                decimal totalRevenue = completedOrders.Sum(o => o.FinalPrice);
                stats.PieceworkEarnings = totalRevenue * (currentEmployee.BaseWagePercentage / 100m);

                // Суммируем все бонусы «Умного кассира» за смену
                stats.UpsellBonusTotal = completedOrders.Sum(o => ExtractUpsellBonus(o.Notes));
            }

            return stats;
        }
    }

    public class OrderCalculation
    {
        public decimal ServicesTotal { get; set; }
        public decimal FinalPrice { get; set; }
        public decimal WasherEarnings { get; set; }
        public decimal UpsellBonus { get; set; } // Поля для апселла
        public decimal CompanyEarnings { get; set; }
    }

    /// <summary>
    /// Универсальный расчет ЗП за смену для любой роли
    /// </summary>
    public class EmployeeShiftStats
    {
        public decimal FixedSalary { get; set; }       // Оклад за выход
        public decimal PieceworkEarnings { get; set; } // Проценты (от машин или общей кассы)
        public decimal UpsellBonusTotal { get; set; }  // Бонусы Умного кассира

        // Всего начислено за день
        public decimal TotalEarned => FixedSalary + PieceworkEarnings + UpsellBonusTotal;

        public decimal AdvancesTotal { get; set; }    // Взятые авансы
        public decimal PayoutAmount => System.Math.Max(0, TotalEarned - AdvancesTotal); // К выдаче на руки
    }
}