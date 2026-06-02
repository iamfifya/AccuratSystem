using System;
using System.Runtime.InteropServices;

namespace AccuratPanelCWM.Services
{
    public static class TimeHelper
    {
        // Умное определение: Windows или Unix (Android/iOS)
        private static readonly string MskZoneId = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Russian Standard Time"
            : "Europe/Moscow";

        private static readonly TimeZoneInfo MskZone = TimeZoneInfo.FindSystemTimeZoneById(MskZoneId);

        public static DateTime NowMsk => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, MskZone);

        public static DateTime ToMsk(DateTime serverTime)
        {
            if (serverTime.Kind == DateTimeKind.Local) serverTime = serverTime.ToUniversalTime();
            DateTime utcTime = DateTime.SpecifyKind(serverTime, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, MskZone);
        }

        public static DateTime ToUtc(DateTime mskTime)
        {
            if (mskTime.Kind == DateTimeKind.Utc) return mskTime;
            DateTime unspecifiedTime = DateTime.SpecifyKind(mskTime, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecifiedTime, MskZone);
        }
    }
}