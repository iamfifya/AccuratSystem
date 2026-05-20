using AccuratSystem.Contracts.Models;
using System.Collections.Generic;
using System.Linq;
using AccuratPanelCarWashing.Services; // <-- ВАЖНО: для метода расширения GetWasherId()

namespace AccuratPanelCarWashing.Services
{
    /// <summary>
    /// ЕДИНСТВЕННЫЙ источник истины для расчётов заказов и зарплат.
    /// </summary>
    public static class OrderMath
    {
        // === НАСТРОЙКИ (меняй только здесь) ===
        public const decimal WASHER_PERCENT = 0.35m;          // 35% мойщику
        public const decimal MIN_WASHER_PAY_PER_SHIFT = 1000m; // Мин. ЗП за смену

        /// <summary>
        /// Рассчитывает ВСЕ значения для одного заказа.
        /// </summary>
        // ИСПРАВЛЕНО: Сделали washers необязательным параметром (по умолчанию null)
        public static OrderCalculation Calculate(Order order, List<Service> allServices, List<User> washers = null)
        {
            var calc = new OrderCalculation();
            if (order == null) return calc;

            // 1. Находим выбранного мойщика и его базовую ставку (по умолчанию 35%)
            // ИСПРАВЛЕНО: Используем метод расширения GetWasherId() вместо order.WasherId
            var washer = washers?.FirstOrDefault(w => w.Id == order.GetWasherId());
            decimal basePercentage = washer?.BaseWagePercentage ?? 35m;

            decimal servicesTotal = 0;
            decimal washerEarnings = 0;

            // 2. Считаем услуги
            if (order.ServiceIds != null && allServices != null)
            {
                foreach (var sid in order.ServiceIds)
                {
                    var svc = allServices.FirstOrDefault(s => s.Id == sid);
                    if (svc != null)
                    {
                        // ИСПРАВЛЕНО: Вычисляем цену вручную через PriceByBodyType вместо svc.GetPrice()
                        decimal price = svc.PriceByBodyType.TryGetValue(order.BodyTypeCategory, out var p)
                            ? p
                            : (svc.PriceByBodyType.TryGetValue(1, out var def) ? def : 0);

                        servicesTotal += price;

                        // МАГИЯ: Берем кастомный % услуги или (если его нет) базовый % мойщика
                        decimal activePercentage = svc.CustomWagePercentage ?? basePercentage;
                        washerEarnings += price * (activePercentage / 100m);
                    }
                }
            }

            calc.ServicesTotal = servicesTotal;

            // 3. Добавляем ExtraCost (доп. услуги всегда считаются по базовой ставке мойщика)
            decimal baseAmount = servicesTotal + order.ExtraCost;
            if (order.ExtraCost > 0)
            {
                washerEarnings += order.ExtraCost * (basePercentage / 100m);
            }

            // 4. Применяем скидки к итоговой цене клиента
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

            // 5. Формируем итоговые заработки
            calc.WasherEarnings = washerEarnings;
            calc.CompanyEarnings = calc.FinalPrice - calc.WasherEarnings;

            return calc;
        }

        /// <summary>
        /// Формирует полный расчет зарплаты мойщика за смену (с учетом авансов и минималки).
        /// </summary>
        // ИСПРАВЛЕНО: Добавили проброс параметра washers
        public static WasherShiftStats CalculateShiftStats(
                      IEnumerable<Order> completedOrders,
                      List<Service> allServices,
                      List<User> washers = null,
                      decimal advancesTaken = 0m,
                      bool isWasherAdmin = false)
        {
            // 1. Считаем чистые заработанные проценты
            decimal basePay = completedOrders.Sum(o => Calculate(o, allServices, washers).WasherEarnings);

            return new WasherShiftStats
            {
                BaseEarnings = basePay,
                MinWageTopUp = 0m, // Доплата до минималки отключена
                AdvancesTotal = advancesTaken
            };
        }

        // Оставили для обратной совместимости старых методов (если где-то еще вызывается)
        public static decimal CalculateWasherShiftPay(
            IEnumerable<Order> completedOrders,
            List<Service> allServices,
            List<User> washers = null,
            bool isWasherAdmin = false)
        {
            var stats = CalculateShiftStats(completedOrders, allServices, washers, 0, isWasherAdmin);
            return stats.TotalEarned;
        }
    }

    /// <summary>
    /// Готовый результат расчёта одного заказа.
    /// </summary>
    public class OrderCalculation
    {
        public decimal ServicesTotal { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal ActualDiscount { get; set; }
        public decimal FinalPrice { get; set; }
        public decimal WasherEarnings { get; set; }
        public decimal CompanyEarnings { get; set; }
    }

    /// <summary>
    /// Полный расклад по зарплате мойщика за смену.
    /// </summary>
    public class WasherShiftStats
    {
        public decimal BaseEarnings { get; set; }     // Заработал 35% от заказов
        public decimal MinWageTopUp { get; set; }     // Доплата от компании до 1000 руб
        public decimal TotalEarned => BaseEarnings + MinWageTopUp; // Всего начислено ЗП

        public decimal AdvancesTotal { get; set; }    // Сумма взятых за день авансов

        // Сколько Анне нужно выдать наличкой из кассы при закрытии смены
        public decimal PayoutAmount => System.Math.Max(0, TotalEarned - AdvancesTotal);
    }
}