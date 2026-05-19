namespace AccuratSystem.Contracts.Enums
{
    /// <summary>
    /// Категория услуги. Используется для фильтрации в интерфейсах.
    /// Wash - только автомойка
    /// Service - только автосервис
    /// Both - доступно для обоих направлений
    /// </summary>
    public enum ServiceCategory
    {
        Wash,
        Service,
        Both
    }
}