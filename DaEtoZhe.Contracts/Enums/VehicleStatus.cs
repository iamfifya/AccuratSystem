namespace DaEtoZhe.Contracts.Enums
{
    /// <summary>Статус автомобиля в жизненном цикле.</summary>
    public enum VehicleStatus
    {
        New = 0,           // Новый (только что добавлен)
        Purchase = 1,      // Куплен (ожидает оценки)
        Appraisal = 2,     // Оценен (ожидает ремонта)
        Repair = 3,        // В ремонте
        Prep = 4,          // Предпродажная подготовка
        Listed = 5,        // Выставлен на продажу
        Reserved = 6,      // Зарезервирован (клиент забронировал)
        Sold = 7,          // Продан
        WrittenOff = 8     // Списан (утилизация/разборка)
    }
}