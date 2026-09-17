using NodaTime;
using System;

namespace Accurat.WebAPI.Time
{
    /// <summary>
    /// Конвенция времени v1: в БД хранится настенное время филиала,
    /// помеченное как UTC (наследие SpecifyKind). Вся бизнес-семантика
    /// (день, час, границы смены) определяется в зоне филиала.
    /// </summary>
    public static class BusinessTime
    {
        /// <summary>Хранимый DateTime -> настенное время (LocalDateTime) в зоне филиала.</summary>
        public static LocalDateTime ToWallClock(this DateTime stored) =>
            LocalDateTime.FromDateTime(stored);

        /// <summary>Настенное время -> хранимый формат (legacy: Kind=Utc).</summary>
        public static DateTime ToStored(this LocalDateTime wall) =>
            DateTime.SpecifyKind(wall.ToDateTimeUnspecified(), DateTimeKind.Utc);

        /// <summary>Бизнес-день события (дата в зоне филиала).</summary>
        public static LocalDate BusinessDay(this DateTime stored) =>
            stored.ToWallClock().Date;

        /// <summary>Час суток (0-23) в зоне филиала.</summary>
        public static int BusinessHour(this DateTime stored) =>
            stored.ToWallClock().Hour;

        /// <summary>
        /// Диапазон запроса для включительных бизнес-дней [from..to]
        /// в координатах хранения: 00:00 первого дня .. 00:00 следующего после последнего минус 1 tick.
        /// </summary>
        public static (DateTime Start, DateTime End) StoredRange(LocalDate from, LocalDate to)
        {
            var start = from.At(LocalTime.Midnight).ToStored();
            var end = to.PlusDays(1).At(LocalTime.Midnight).ToStored().AddTicks(-1);
            return (start, end);
        }

        /// <summary>То же из параметров запроса (берём только дату, отбрасывая время).</summary>
        public static (DateTime Start, DateTime End) StoredRangeInclusive(DateTime start, DateTime end) =>
            StoredRange(start.ToWallClock().Date, end.ToWallClock().Date);

        // ═══════════════════════════════════════════════════════
        //  ZONE-AWARE МЕТОДЫ (для v2, когда появятся Instant)
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// [v2] Конвертирует Instant в настенное время в указанной зоне.
        /// Пока не используется — подготовка к миграции DateTime → Instant.
        /// </summary>
        public static LocalDateTime ToWallClock(this Instant instant, DateTimeZone zone) =>
            instant.InZone(zone).LocalDateTime;

        /// <summary>
        /// [v2] Бизнес-день из Instant в указанной зоне.
        /// </summary>
        public static LocalDate BusinessDay(this Instant instant, DateTimeZone zone) =>
            instant.InZone(zone).Date;

        /// <summary>
        /// [v2] Час суток из Instant в указанной зоне.
        /// </summary>
        public static int BusinessHour(this Instant instant, DateTimeZone zone) =>
            instant.InZone(zone).Hour;

        /// <summary>[v1] Instant -> настенное LocalDateTime (legacy: UTC-поля = локальные поля филиала).</summary>
        public static LocalDateTime ToWallClock(this Instant instant) =>
            LocalDateTime.FromDateTime(instant.ToDateTimeUtc());

        /// <summary>[v1] Бизнес-день из Instant по конвенции v1 (без сдвига зоной).</summary>
        public static LocalDate BusinessDay(this Instant instant) =>
            instant.ToWallClock().Date;

        /// <summary>[v1] Час суток (0-23) из Instant по конвенции v1 (без сдвига зоной).</summary>
        public static int BusinessHour(this Instant instant) =>
            instant.ToWallClock().Hour;

        /// <summary>
        /// [v2] Диапазон запроса для включительных бизнес-дней в указанной зоне.
        /// Возвращает Instant-границы для честного хранения времени.
        /// </summary>
        public static (Instant Start, Instant End) StoredRangeInstant(
            LocalDate from, LocalDate to, DateTimeZone zone)
        {
            var start = from.AtStartOfDayInZone(zone).ToInstant();
            var end = to.PlusDays(1).AtStartOfDayInZone(zone).ToInstant().Minus(Duration.FromTicks(1));
            return (start, end);
        }

        /// <summary>
        /// [v1.5] Интерпретация legacy-значения (настенное время филиала, помеченное как UTC)
        /// как ZonedDateTime в зоне филиала. Значение НЕ сдвигается: поля остаются теми же.
        /// После миграции данных (Этап 6) заменяется на instant.InZone(zone).
        /// </summary>
        public static ZonedDateTime FromLegacyWallClock(this DateTime stored, DateTimeZone zone) =>
            LocalDateTime.FromDateTime(stored).InZoneLeniently(zone);

        /// <summary>[v1.5] Бизнес-день в зоне филиала (legacy-интерпретация).</summary>
        public static LocalDate BusinessDay(this DateTime stored, DateTimeZone zone) =>
            stored.FromLegacyWallClock(zone).Date;

        /// <summary>[v1.5] Час суток (0-23) в зоне филиала (legacy-интерпретация).</summary>
        public static int BusinessHour(this DateTime stored, DateTimeZone zone) =>
            stored.FromLegacyWallClock(zone).Hour;
    }
}