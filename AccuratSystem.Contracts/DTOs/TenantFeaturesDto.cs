namespace AccuratSystem.Contracts.DTOs
{
    /// <summary>
    /// Набор доступных DLC-модулей для филиала.
    /// </summary>
    public class TenantFeaturesDto
    {
        public bool IsUpsellEnabled { get; set; }
        public bool IsStorageEnabled { get; set; }
        public bool IsCrmMarketingEnabled { get; set; }
        public bool IsTelegramBossEnabled { get; set; }
    }
}
