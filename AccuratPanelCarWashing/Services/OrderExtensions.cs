using AccuratSystem.Contracts.Models;
using System.Linq;

namespace AccuratPanelCarWashing.Services
{
    /// <summary>
    /// Методы расширения для обратной совместимости со старым кодом.
    /// Позволяют использовать o.WasherId вместо работы с коллекцией OrderWashers.
    /// </summary>
    public static class OrderExtensions
    {
        /// <summary>
        /// Возвращает ID первого мойщика из заказа (для старого UI).
        /// </summary>
        public static int? GetWasherId(this Order order)
        {
            if (order == null) return null;
            if (order.OrderWashers == null || order.OrderWashers.Count == 0) return null;
            return order.OrderWashers.FirstOrDefault()?.UserId;
        }

        /// <summary>
        /// Устанавливает мойщика в заказ (для старого UI).
        /// </summary>
        public static void SetWasherId(this Order order, int? washerId)
        {
            if (order == null) return;

            if (order.OrderWashers == null)
                order.OrderWashers = new System.Collections.Generic.List<OrderWasher>();

            order.OrderWashers.Clear();

            if (washerId.HasValue && washerId.Value > 0)
            {
                order.OrderWashers.Add(new OrderWasher
                {
                    UserId = washerId.Value,
                    SplitShare = 1.0m
                });
            }
        }
    }
}