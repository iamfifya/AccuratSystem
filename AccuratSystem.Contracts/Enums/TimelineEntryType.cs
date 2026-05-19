namespace AccuratSystem.Contracts.Enums
{
    /// <summary>
    /// Тип записи в ленте событий заказа.
    /// Позволяет визуально разделять комментарии, изменения цен и смену статусов.
    /// </summary>
    public enum TimelineEntryType
    {
        Comment,
        StatusChanged,
        PriceChanged,
        ExpenseAdded
    }
}