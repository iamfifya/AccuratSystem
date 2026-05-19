namespace AccuratSystem.Contracts.Enums
{
    /// <summary>
    /// Статусы выполнения заказа. Расширены под реалии автосервиса.
    /// Конвертеры на клиенте будут маппить эти значения в текст и цвета.
    /// </summary>
    public enum OrderStatus
    {
        InProgress,
        WaitingForParts,
        Diagnostics,
        ReadyForPickup,
        Completed,
        Cancelled
    }
}