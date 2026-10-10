namespace DaEtoZhe.Contracts.Enums
{
    /// <summary>Зона автомобиля, где найден дефект.</summary>
    public enum DefectArea
    {
        Body = 0,         // кузов
        Engine = 1,       // двигатель
        Transmission = 2, // трансмиссия
        Suspension = 3,   // подвеска
        Electrical = 4,   // электрика
        Salon = 5,        // салон
        Glass = 6,        // стёкла
        Wheels = 7,       // колёса
        Other = 8         // прочее
    }

    /// <summary>Критичность дефекта.</summary>
    public enum DefectSeverity
    {
        Minor = 0,    // мелкий (косметика)
        Major = 1,    // средний (узлы/агрегаты)
        Critical = 2  // критичный (безопасность/геометрия)
    }
}