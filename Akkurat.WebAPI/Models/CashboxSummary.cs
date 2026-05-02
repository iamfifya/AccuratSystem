// Akkurat.WebAPI/Models/CashboxSummary.cs
namespace Akkurat.WebAPI.Models
{
    public class CashboxSummary
    {
        public decimal CashInHand { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetCashProfit { get; set; }
    }
}