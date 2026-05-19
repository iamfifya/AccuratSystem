namespace AccuratSystem.Contracts.DTOs
{
    /// <summary>
    /// DTO для обновления цены услуги в заказе.
    /// </summary>
    public class UpdateServicePriceDto
    {
        public int OrderServiceItemId { get; set; }
        public decimal NewPrice { get; set; }
        public string Note { get; set; } = string.Empty;
        public string UpdatedByUser { get; set; } = string.Empty;
    }
}