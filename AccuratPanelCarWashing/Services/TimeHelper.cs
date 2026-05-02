using System;

namespace AccuratPanelCarWashing.Services
{
    public static class TimeHelper
    {
        private static readonly TimeZoneInfo MskZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");

        public static DateTime NowMsk => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, MskZone);

        public static DateTime ToMsk(DateTime serverTime)
        {
            // Если прилетело Local, сначала честно переводим в UTC
            if (serverTime.Kind == DateTimeKind.Local)
            {
                serverTime = serverTime.ToUniversalTime();
            }

            // Жестко помечаем время как UTC, чтобы конвертер не сомневался
            DateTime utcTime = DateTime.SpecifyKind(serverTime, DateTimeKind.Utc);

            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, MskZone);
        }

        public static DateTime ToUtc(DateTime mskTime)
        {
            if (mskTime.Kind == DateTimeKind.Utc)
            {
                return mskTime;
            }

            // СБРОС Kind: делаем время "неопределенным", снимая метку Local.
            // Теперь конвертер без лишних вопросов поверит, что это время MskZone.
            DateTime unspecifiedTime = DateTime.SpecifyKind(mskTime, DateTimeKind.Unspecified);

            return TimeZoneInfo.ConvertTimeToUtc(unspecifiedTime, MskZone);
        }
    }
}
