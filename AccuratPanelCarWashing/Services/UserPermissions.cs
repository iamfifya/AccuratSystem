using AccuratSystem.Contracts.Models;

namespace AccuratPanelCarWashing.Services
{
    public static class UserPermissions
    {
        // Условие А: Доступно всем авторизованным пользователям
        public static bool CanAccessBasicFeatures(User user)
            => user != null;

        // Условие В: Доступ только Администраторам и Директору (Менеджеры)
        public static bool IsManagement(User user)
            => user != null && (user.Role == (int)UserRole.Director || user.Role == (int)UserRole.Administrator);

        // Условие Б: Только Директор
        public static bool IsSuperUser(User user)
            => user != null && user.Role == (int)UserRole.Director;

        // Дополнительно: доступ только для рабочих (мойщики + сервис)
        public static bool IsStaff(User user)
            => user != null && (user.Role == (int)UserRole.Washer || user.Role == (int)UserRole.ServiceStaff);
    }
}
