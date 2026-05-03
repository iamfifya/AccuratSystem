using AccuratPanelCarWashing.Models;
using System.Collections.Generic;
using System.Linq;

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
        public static OrderCalculation Calculate(CarWashOrder order, List<Service> allServices)
        {
            // 1. Сумма услуг по актуальным ценам
            decimal servicesTotal = (order.ServiceIds ?? new List<int>())
                .Sum(sid => allServices?.FirstOrDefault(s => s.Id == sid)?.GetPrice(order.BodyTypeCategory) ?? 0);

            // 2. База: услуги + доп. расходы
            decimal baseAmount = servicesTotal + order.ExtraCost;

            // 3. Скидка (применяется ко ВСЕЙ базе)
            decimal actualDiscount = order.DiscountPercent > 0
                ? baseAmount * (order.DiscountPercent / 100m)
                : order.DiscountAmount;

            // 4. Результаты
            return new OrderCalculation
            {
                ServicesTotal = servicesTotal,
                BaseAmount = baseAmount,
                ActualDiscount = actualDiscount,
                FinalPrice = baseAmount - actualDiscount,
                WasherEarnings = baseAmount * WASHER_PERCENT, // Скидка НЕ влияет
                CompanyEarnings = (baseAmount - actualDiscount) - (baseAmount * WASHER_PERCENT)
            };
        }

        /// <summary>
        /// Формирует полный расчет зарплаты мойщика за смену (с учетом авансов и минималки).
        /// </summary>
        public static WasherShiftStats CalculateShiftStats(
                      IEnumerable<CarWashOrder> completedOrders,
                      List<Service> allServices,
                      decimal advancesTaken = 0m,
                      bool isWasherAdmin = false)
        {
            // 1. Считаем чистые заработанные 35%
            decimal basePay = completedOrders.Sum(o => Calculate(o, allServices).WasherEarnings);

            return new WasherShiftStats
            {
                BaseEarnings = basePay,
                MinWageTopUp = 0m, // Доплата до минималки отключена
                AdvancesTotal = advancesTaken
            };
        }

        // Оставили для обратной совместимости старых методов (если где-то еще вызывается)
        public static decimal CalculateWasherShiftPay(
            IEnumerable<CarWashOrder> completedOrders,
            List<Service> allServices,
            bool isWasherAdmin = false)
        {
            var stats = CalculateShiftStats(completedOrders, allServices, 0, isWasherAdmin);
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
