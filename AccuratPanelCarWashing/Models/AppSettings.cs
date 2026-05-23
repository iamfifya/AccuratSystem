using System;
using System.IO;
using Newtonsoft.Json;

namespace AccuratPanelCarWashing.Models
{
    public class AppSettings
    {
        // === НАСТРОЙКИ (сериализуются) ===
        public bool AutoBackup { get; set; } = true;
        public int BackupDaysToKeep { get; set; } = 7;
        public int LogDaysToKeep { get; set; } = 30;
        public string DefaultPaymentMethod { get; set; } = "Наличные";
        public decimal DefaultWasherPercent { get; set; } = 35;
        public bool RequireConfirmationForDelete { get; set; } = true;
        public DateTime LastSettingsChange { get; set; }

        // === ФИЛИАЛ — ПРОСТЫЕ STATIC-ПОЛЯ (как было изначально) ===
        // ⚠️ НЕ СЕРИАЛИЗУЮТСЯ, но это ОК — филиал хранится в сессии пользователя
        public static int CurrentBranchId { get; set; }
        public static string CurrentBranchName { get; set; }
        public static int CurrentBranchWashBaysCount { get; set; }
        public static int CurrentBranchServiceLiftsCount { get; set; }

        private static string SettingsPath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                LastSettingsChange = DateTime.Now;
                string json = JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }
}