using AccuratSystem.Contracts.Enums;
using AccuratSystem.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AccuratSystem.Contracts.Models // ОБЩИЙ НЕЙМСПЕЙС
{
    public static class OrderMath
    {
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
                    if (decimal.TryParse(bonusStr, out decimal bonus)) return bonus;
                }
            }
            catch { }
            return 0;
        }

        public static OrderCalculation Calculate(Order order, List<Service> allServices, List<User> washers = null, CompanySettings settings = null, ShiftType shiftType = ShiftType.Day)
        {
            var calc = new OrderCalculation();
            if (order == null) return calc;

            decimal companySharePercent = settings?.CompanySharePercentage ?? 65m;

            // Определяем базовый процент мойщика
            int? washerId = order.OrderWashers?.FirstOrDefault()?.UserId;
            var washer = washers?.FirstOrDefault(w => w.Id == washerId);
            decimal basePercentage = washer?.BaseWagePercentage ?? 35m;

            decimal servicesTotal = 0;
            decimal washerEarnings = 0;

            // --- ЛОГИКА РАСЧЕТА ЗП МОЙЩИКА ---
            if (order.ServiceIds != null && allServices != null)
            {
                foreach (var sid in order.ServiceIds)
                {
                    var svc = allServices.FirstOrDefault(s => s.Id == sid);
                    if (svc != null)
                    {
                        decimal price = svc.PriceByBodyType.TryGetValue(order.BodyTypeCategory, out var p) ? p : (svc.PriceByBodyType.TryGetValue(1, out var def) ? def : 0);
                        servicesTotal += price;

                        if (shiftType == ShiftType.Night)
                        {
                            // В ночную смену: берем фиксированный % из настроек компании (например 50%)
                            decimal nightPercent = settings?.NightShiftWasherPercentage ?? 50m;
                            washerEarnings += price * (nightPercent / 100m);
                        }
                        else
                        {
                            // В дневную смену: индивидуальный % или % услуги
                            decimal activePercentage = svc.CustomWagePercentage ?? basePercentage;
                            washerEarnings += price * (activePercentage / 100m);
                        }
                    }
                }
            }

            calc.ServicesTotal = servicesTotal;
            decimal baseAmount = servicesTotal + order.ExtraCost;

            // Доплата за ExtraCost
            if (order.ExtraCost > 0)
            {
                decimal extraPercent = (shiftType == ShiftType.Night)
                    ? (settings?.NightShiftWasherPercentage ?? 50m)
                    : basePercentage;
                washerEarnings += order.ExtraCost * (extraPercent / 100m);
            }

            decimal actualDiscount = 0;
            if (order.DiscountPercent > 0) actualDiscount = baseAmount * (order.DiscountPercent / 100m);
            else if (order.DiscountAmount > 0) actualDiscount = order.DiscountAmount;

            calc.FinalPrice = baseAmount - actualDiscount;
            calc.UpsellBonus = ExtractUpsellBonus(order.Notes);
            calc.WasherEarnings = washerEarnings; // Теперь тут будет либо 50%, либо индивидуальный %
            calc.CompanyGrossEarnings = calc.FinalPrice * (companySharePercent / 100m);
            calc.CompanyNetEarnings = calc.CompanyGrossEarnings - calc.UpsellBonus;

            return calc;
        }

        public static EmployeeShiftStats CalculateShiftStats(IEnumerable<Order> branchOrdersForShift, List<Service> allServices, User currentEmployee, ShiftType shiftType, List<User> allUsers = null, CompanySettings settings = null, decimal advancesTaken = 0m)
        {
            var stats = new EmployeeShiftStats();
            if (currentEmployee == null) return stats;

            stats.AdvancesTotal = advancesTaken;

            if (currentEmployee.RoleId == 3 || currentEmployee.RoleId == 4) // МОЙЩИКИ / СЕРВИС
            {
                var myOrders = branchOrdersForShift.Where(o => o.OrderWashers?.FirstOrDefault()?.UserId == currentEmployee.Id).ToList();
                // Используем обновленный Calculate с учетом типа смены!
                stats.PieceworkEarnings = myOrders.Sum(o => Calculate(o, allServices, allUsers, settings, shiftType).WasherEarnings);
            }
            else // 👑 АДМИН / ДИРЕКТОР
            {
                stats.FixedSalary = currentEmployee.BaseSalaryPerShift;
                var completedOrders = branchOrdersForShift.Where(o => o.Status == "Выполнен" || o.Status == "Завершен").ToList();
                decimal totalRevenue = completedOrders.Sum(o => o.FinalPrice);

                if (shiftType == ShiftType.Day)
                {
                    // ТЕ САМЫЕ 2.5% от общего оборота за день
                    decimal adminDayPercent = settings?.DayShiftAdminPercentage ?? 2.5m;
                    stats.PieceworkEarnings = totalRevenue * (adminDayPercent / 100m);
                }
                else
                {
                    // Ночью админ может получать либо 0, либо свою базовую ставку (зависит от договоренности)
                    stats.PieceworkEarnings = totalRevenue * (currentEmployee.BaseWagePercentage / 100m);
                }

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
        public decimal UpsellBonus { get; set; }
        public decimal CompanyGrossEarnings { get; set; }
        public decimal CompanyNetEarnings { get; set; }

        // Поле для совместимости со старым кодом MainWindow (CompanyEarnings)
        public decimal CompanyEarnings => CompanyNetEarnings;
    }

    public class EmployeeShiftStats
    {
        public decimal FixedSalary { get; set; }
        public decimal PieceworkEarnings { get; set; }
        public decimal UpsellBonusTotal { get; set; }
        public decimal TotalEarned => FixedSalary + PieceworkEarnings + UpsellBonusTotal;
        public decimal AdvancesTotal { get; set; }
        public decimal PayoutAmount => System.Math.Max(0, TotalEarned - AdvancesTotal);
    }
}