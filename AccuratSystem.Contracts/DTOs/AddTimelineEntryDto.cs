using AccuratSystem.Contracts.Enums;

namespace AccuratSystem.Contracts.DTOs
{
    /// <summary>
    /// DTO для добавления записи в ленту событий.
    /// </summary>
    public class AddTimelineEntryDto
    {
        public int OrderId { get; set; }
        public TimelineEntryType EntryType { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? RelatedEntityId { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
    }
}