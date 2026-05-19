namespace AccuratSystem.Contracts.Enums
{
    /// <summary>
    /// Категории внутренних затрат по заказу.
    /// Фиксированный список для упрощения отчётности.
    /// </summary>
    public enum ExpenseCategory
    {
        Parts,        // Запчасти
        Consumables,  // Расходники (масло, химия, ветошь)
        ExternalWork  // Сторонние работы (например, балансировка на аутсорсе)
    }
}