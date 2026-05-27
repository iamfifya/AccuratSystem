namespace AccuratSystem.Contracts.DTOs
{
    public class UpsellSuggestion
    {
        public int Id { get; set; }
        public int TriggerServiceId { get; set; }
        public int SuggestedServiceId { get; set; }
        public string Message { get; set; }
        public decimal BonusAmount { get; set; }
    }
}
