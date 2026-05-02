using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AccuratPanelCarWashing.Services
{
    public static class Logger
    {
        private static readonly string _logFolder;
        private static readonly object _lockObj = new object();

        // Контекст сессии и пользователя
        public static string SessionId { get; private set; } = Guid.NewGuid().ToString("N").Substring(0, 8);
        private static string _currentUser = "System";
        private static string _currentUserId = "?";

        public static void SetUserContext(string fullName, int? id = null)
        {
            _currentUser = fullName ?? "Неизвестный";
            _currentUserId = id?.ToString() ?? "?";
        }

        static Logger()
        {
            _logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            try { if (!Directory.Exists(_logFolder)) Directory.CreateDirectory(_logFolder); } catch { }
        }

        public static void Info(string message, string category = "APP", string details = "") => Log(message, "INFO", category, details);
        public static void Warn(string message, string category = "APP", string details = "") => Log(message, "WARN", category, details);
        public static void Error(string message, Exception ex, string category = "APP", string details = "")
        {
            string exDetails = $"Исключение: {ex?.GetType().Name}\nСообщение: {ex?.Message}\nСтек:\n{ex?.StackTrace}";
            string fullDetails = string.IsNullOrEmpty(details) ? exDetails : $"{exDetails}\nДополнительно: {details}";
            Log(message, "ERROR", category, fullDetails);
        }

        private static void Log(string message, string level, string category, string details)
        {
            Task.Run(() =>
            {
                try
                {
                    string logFile = Path.Combine(_logFolder, $"app_{DateTime.Now:yyyy-MM-dd}.log");
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string threadId = Thread.CurrentThread.ManagedThreadId.ToString();
                    string userCtx = $"{_currentUser}(ID:{_currentUserId})";
                    string detailStr = string.IsNullOrEmpty(details) ? "" : $" | Детали: {details.Replace(Environment.NewLine, " | ")}";

                    // Формат: TIMESTAMP | [LEVEL] | [CATEGORY] | USER | ACTION | DETAILS | THREAD
                    string entry = $"{timestamp} | [{level}] | [{category}] | {userCtx} | {message}{detailStr} | Th:{threadId}{Environment.NewLine}";

                    lock (_lockObj)
                    {
                        File.AppendAllText(logFile, entry, Encoding.UTF8);
                    }
                    CleanupOldLogs();
                }
                catch { } // Лог никогда не ломает приложение
            });
        }

        private static void CleanupOldLogs(int days = 30)
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-days);
                foreach (var file in Directory.GetFiles(_logFolder, "app_*.log"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (name.StartsWith("app_") && name.Length >= 14)
                    {
                        if (DateTime.TryParse(name.Substring(4, 10), out var d) && d < cutoff)
                            File.Delete(file);
                    }
                }
            }
            catch { }
        }
    }
}
