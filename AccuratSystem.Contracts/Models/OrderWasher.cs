namespace AccuratSystem.Contracts.Models
{
    /// <summary>
    /// Связь заказа с мойщиком (Soft Split).
    /// Навигационные свойства добавлены для EF Core, но в C# 7.3 не используем ?.
    /// Если свойство не загружено через Include — будет null в runtime.
    /// </summary>
    public class OrderWasher
    {
        public int OrderId { get; set; }
        public int UserId { get; set; }
        public decimal SplitShare { get; set; }

        // Навигационные свойства (без ? для совместимости с C# 7.3)
        // При использовании: проверяй на null или используй Include в запросе
        public Order Order { get; set; }
        public User Washer { get; set; }
    }
}