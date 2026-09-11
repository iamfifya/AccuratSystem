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
        ExpenseAdded,
        ShiftTransferred = 100,

        // === НОВОЕ: аудит финансовых изменений заказа ===
        // PriceChanged — итоговая сумма заказа изменилась (любая причина)
        PriceChanged,

        // DiscountApplied — изменён процент или сумма скидки
        DiscountApplied,
        // ExtraCostChanged — изменена доплата или её причина
        ExtraCostChanged,

        // === НОВОЕ: операционные изменения ===
        BoxChanged,           // смена бокса
        WasherChanged,        // смена мойщика
        PaymentMethodChanged, // смена способа оплаты
        TimeChanged           // смена времени записи
    }
}