using Accurat.WebAPI.Models;
using System.Collections.Generic;
using System.Linq;

namespace Accurat.WebAPI.Services
{
    public static class SalaryCalculationService
    {
        public static decimal CalculateWasherIncomeForOrder(OrderWasher orderWasher, Order order, List<Service> allServices)
        {
            if (orderWasher.Washer == null) return 0;

            decimal totalIncome = 0;

            // 1. Считаем ЗП за каждую услугу отдельно
            if (order.ServiceIds != null && order.ServiceIds.Any())
            {
                foreach (var serviceId in order.ServiceIds)
                {
                    var service = allServices.FirstOrDefault(s => s.Id == serviceId);
                    if (service != null && service.PriceByBodyType != null && service.PriceByBodyType.TryGetValue(order.BodyTypeCategory, out decimal servicePrice))
                    {
                        // 🔥 МАГИЯ: Берем кастомный процент услуги, а если его нет — базовый процент сотрудника
                        decimal activePercentage = service.CustomWagePercentage ?? orderWasher.Washer.BaseWagePercentage;

                        totalIncome += (servicePrice * (activePercentage / 100m)) * orderWasher.SplitShare;
                    }
                }
            }

            // 2. Обработка ExtraCost (наценка за сильное загрязнение и т.д.)
            // На нее всегда применяем базовый процент сотрудника
            if (order.ExtraCost > 0)
            {
                totalIncome += (order.ExtraCost * (orderWasher.Washer.BaseWagePercentage / 100m)) * orderWasher.SplitShare;
            }

            return totalIncome;
        }
    }
}