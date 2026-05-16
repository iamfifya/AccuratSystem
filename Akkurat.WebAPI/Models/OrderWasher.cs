namespace Accurat.WebAPI.Models
{
    public class OrderWasher
    {
        public int OrderId { get; set; }
        public Order Order { get; set; } // ИСПРАВЛЕНО: было CarWashOrder

        public int UserId { get; set; }
        public User Washer { get; set; }

        public decimal SplitShare { get; set; }
    }
}