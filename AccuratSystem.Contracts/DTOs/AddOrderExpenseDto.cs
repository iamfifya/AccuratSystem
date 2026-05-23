using AccuratSystem.Contracts.Enums;

namespace AccuratSystem.Contracts.DTOs
{
    /// <summary>
    /// DTO для добавления позиции расхода к заказу.
    /// </summary>
    public class AddOrderExpenseDto
    {
        public int OrderId { get; set; }
        public string Name { get; set; } = string.Empty;
        public ExpenseCategory Category { get; set; }
        public decimal CostPrice { get; set; }
        public decimal ClientPrice { get; set; }
        public int Quantity { get; set; } = 1;
        public string Note { get; set; } = string.Empty;

        public string CreatedByUser { get; set; }
    }
}