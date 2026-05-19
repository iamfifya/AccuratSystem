using System;
using AccuratSystem.Contracts.Enums;

namespace AccuratSystem.Contracts.Models
{
    /// <summary>
    /// Внутренние затраты по заказу (запчасти, расходники, сторонние работы).
    /// Учитывается в себестоимости и формирует итог для клиента.
    /// </summary>
    public class OrderExpense
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string Name { get; set; } = string.Empty;
        public ExpenseCategory Category { get; set; }

        /// <summary>
        /// Себестоимость для бизнеса (за сколько купили/списали со склада).
        /// </summary>
        public decimal CostPrice { get; set; }

        /// <summary>
        /// Цена продажи клиенту (может включать наценку).
        /// </summary>
        public decimal ClientPrice { get; set; }

        public int Quantity { get; set; } = 1;
        public string Note { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Order Order { get; set; }
    }
}