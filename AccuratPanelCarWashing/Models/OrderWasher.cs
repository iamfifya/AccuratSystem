namespace AccuratPanelCarWashing.Models
{
    public class OrderWasher
    {
        public int OrderId { get; set; }
        // Саму ссылку на Order здесь не делаем, чтобы избежать цикличности JSON на клиенте

        public int UserId { get; set; }
        // User Washer тоже пока не обязателен для отправки на сервер

        public decimal SplitShare { get; set; }
    }
}