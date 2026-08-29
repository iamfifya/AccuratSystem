using AccuratSystem.Contracts.Enums;
using AccuratSystem.Contracts.Models;
using System.Collections.Generic;
using System.Linq;

namespace Accurat.WebAPI.Services
{
    public static class SalaryCalculationService
    {
        // ДОБАВИЛИ ПАРАМЕТР: List<User> allUsers
        public static decimal CalculateWasherIncomeForOrder(OrderWasher orderWasher, Order order, List<Service> allServices, List<User> allUsers, ShiftType shiftType, CompanySettings settings)
        {
            // Просто вызываем единый математический центр системы
            var calc = OrderMath.Calculate(order, allServices, allUsers, settings, shiftType);

            // Возвращаем долю конкретного мойщика (если их несколько на заказе)
            return calc.WasherEarnings * orderWasher.SplitShare;
        }

    }
}