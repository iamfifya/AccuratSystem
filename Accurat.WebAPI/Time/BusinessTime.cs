using NodaTime;
using System;

namespace Accurat.WebAPI.Time
{
    /// <summary>
    /// Конвенция v2: хранилище и провод содержат абсолютные моменты (UTC, Kind=Utc).
    /// Любая бизнес-семантика (день, час, границы суток) извлекается ТОЛЬКО
    /// через зону филиала. Raw .Date/.Hour/SpecifyKind запрещены.
    /// </summary>
    public static class BusinessTime
    {
        /// <summary>DateTime (из хранилища/провода) -> ZonedDateTime в зоне филиала.</summary>
        public static ZonedDateTime InBranchZone(this DateTime utcInstant, DateTimeZone zone) =>
            Instant.FromDateTimeUtc(Normalize(utcInstant)).InZone(zone);

        /// <summary>
        /// Приводит любой Kind к Utc с сохранением момента:
        /// Utc — как есть; Local/Unspecified — через ToUniversalTime (серверная TZ).
        /// </summary>
        private static DateTime Normalize(DateTime dt) =>
            dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();

        /// <summary>Бизнес-день события в зоне филиала.</summary>
        public static LocalDate BusinessDay(this DateTime utcInstant, DateTimeZone zone) =>
            utcInstant.InBranchZone(zone).Date;

        /// <summary>Час суток (0-23) в зоне филиала.</summary>
        public static int BusinessHour(this DateTime utcInstant, DateTimeZone zone) =>
            utcInstant.InBranchZone(zone).Hour;

        /// <summary>Включительный диапазон бизнес-дней [from..to] в координатах хранения (UTC).</summary>
        public static (DateTime StartUtc, DateTime EndUtc) StoredRangeInclusive(
            LocalDate from, LocalDate to, DateTimeZone zone)
        {
            var start = from.AtStartOfDayInZone(zone).ToInstant().ToDateTimeUtc();
            var end = to.PlusDays(1).AtStartOfDayInZone(zone).ToInstant().ToDateTimeUtc().AddTicks(-1);
            return (start, end);
        }

        /// <summary>То же из параметров запроса (моменты UTC): берём бизнес-дни в зоне филиала.</summary>
        public static (DateTime StartUtc, DateTime EndUtc) StoredRangeInclusive(
            DateTime fromUtc, DateTime toUtc, DateTimeZone zone) =>
            StoredRangeInclusive(fromUtc.InBranchZone(zone).Date, toUtc.InBranchZone(zone).Date, zone);

        /// <summary>LocalDate -> момент-полночь в зоне филиала (для DTO-дат на провод).</summary>
        public static DateTime ToWireDate(this LocalDate date, DateTimeZone zone) =>
            date.AtStartOfDayInZone(zone).ToInstant().ToDateTimeUtc();

        /// <summary>
        /// Приводит входящий DateTime к UTC-инстанту с сохранением момента:
        /// Utc — как есть; Local/Unspecified — через ToUniversalTime (TZ сервера = TZ филиала).
        /// </summary>
        public static DateTime ToInstantUtc(DateTime dt) =>
            dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
    }
}