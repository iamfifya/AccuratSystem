using AccuratSystem.Contracts.DTOs;

namespace AccuratPanelCarWashing.Services
{
    public static class UserSession
    {
        // Здесь храним флаги DLC для текущего филиала
        public static TenantFeaturesDto Features { get; set; }

        // Вспомогательный метод для быстрой проверки (C# 7.3)
        public static bool IsFeatureEnabled(System.Func<TenantFeaturesDto, bool> predicate)
        {
            if (Features == null) return false;
            return predicate(Features);
        }
    }
}
