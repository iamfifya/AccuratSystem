using DocumentFormat.OpenXml.Packaging;
using MyPanelCarWashing.Models;
using MyPanelCarWashing.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class FileDataService
{
    private static readonly object _saveLock = new object();
    private static string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appdata.json");
    private static string DataPath => dataPath; // Добавляем свойство для обратной совместимости

    public static void SaveData(AppData data)
    {
        lock (_saveLock)
        {
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            var tempPath = DataPath + ".tmp";

            try
            {
                // 1. Сохраняем во временный файл
                File.WriteAllText(tempPath, json);

                // 2. Проверяем, что запись прошла успешно
                var testRead = File.ReadAllText(tempPath);
                if (string.IsNullOrEmpty(testRead))
                    throw new IOException("Temp file is empty");

                // 3. Создаём резервную копию существующего файла
                if (File.Exists(DataPath))
                {
                    var backupPath = DataPath + ".backup";
                    File.Copy(DataPath, backupPath, true);
                }

                // 4. Перемещаем временный файл
                File.Copy(tempPath, DataPath, true);

                // 5. Удаляем временный файл
                File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                // Пытаемся восстановиться из бэкапа
                if (File.Exists(DataPath + ".backup"))
                {
                    File.Copy(DataPath + ".backup", DataPath, true);
                }
                throw new IOException($"Failed to save data: {ex.Message}", ex);
            }
        }
    }
    public static void CreateAutoBackup()
    {
        try
        {
            string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appdata.json");
            string backupsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");

            if (!Directory.Exists(backupsFolder))
                Directory.CreateDirectory(backupsFolder);

            if (File.Exists(dataPath))
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                string backupPath = Path.Combine(backupsFolder, $"appdata_{timestamp}.json");

                File.Copy(dataPath, backupPath, true);

                // Логируем создание бэкапа
                Logger.Info($"Бэкап создан | Файл: appdata_{timestamp}.json", "BACKUP");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Ошибка при создании бэкапа", ex, "BACKUP");
        }
    }
    public static void CleanupOldBackups(int daysToKeep = 7)
    {
        try
        {
            string backupsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");

            if (!Directory.Exists(backupsFolder))
                return;

            var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
            var deletedCount = 0;

            // Ищем все файлы с именем appdata_*.json
            var backupFiles = Directory.GetFiles(backupsFolder, "appdata_*.json");

            foreach (var file in backupFiles)
            {
                var fileInfo = new FileInfo(file);

                // Сравниваем по дате создания файла
                if (fileInfo.CreationTime < cutoffDate)
                {
                    File.Delete(file);
                    deletedCount++;
                    System.Diagnostics.Debug.WriteLine($"[BACKUP] Удалён старый бэкап: {fileInfo.Name} ({fileInfo.CreationTime:dd.MM.yyyy})");
                }
            }

            if (deletedCount > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[BACKUP] Очистка завершена: удалено {deletedCount} файлов старше {daysToKeep} дн.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BACKUP] Ошибка при очистке: {ex.Message}");
            // Не выбрасываем исключение, чтобы не ломать работу приложения
        }
    }

    public static AppData LoadData()
    {
        if (!File.Exists(dataPath))
        {
            var defaultData = new AppData();

            // Добавляем тестового администратора
            defaultData.Users.Add(new User
            {
                Id = defaultData.GetNextUserId(),
                Login = "1",
                Password = "1",
                FullName = "Анна",
                IsAdmin = true
            });

            defaultData.Users.Add(new User
            {
                Id = defaultData.GetNextUserId(),
                Login = "1",
                Password = "1",
                FullName = "Анастасия",
                IsAdmin = true
            });

            // Добавляем тестового сотрудника
            defaultData.Users.Add(new User
            {
                Id = defaultData.GetNextUserId(),
                Login = "1",
                Password = "1",
                FullName = "переименуй меня",
                IsAdmin = false
            });

            // Загружаем услуги из CSV
            LoadServicesFromCsv(defaultData);

            SaveData(defaultData);
            return defaultData;
        }

        var json = File.ReadAllText(dataPath);
        return JsonConvert.DeserializeObject<AppData>(json);
    }

    private static void LoadServicesFromCsv(AppData data)
    {
        // Основные услуги (Лист1)
        var mainServices = new Dictionary<string, Dictionary<int, decimal>>
    {
        { "Техническая мойка", new Dictionary<int, decimal> { { 1, 700 }, { 2, 750 }, { 3, 850 }, { 4, 900 } } },
        { "Профессиональная мойка кузова", new Dictionary<int, decimal> { { 1, 1100 }, { 2, 1300 }, { 3, 1500 }, { 4, 1800 } } },
        { "Комплекс \"ИЗИ\"", new Dictionary<int, decimal> { { 1, 1900 }, { 2, 2100 }, { 3, 2400 }, { 4, 2700 } } },
        { "Комплекс \"Глянец\"", new Dictionary<int, decimal> { { 1, 2900 }, { 2, 3100 }, { 3, 3400 }, { 4, 3700 } } }
    };

        // Дополнительные услуги (Лист2)
        var extraServices = new Dictionary<string, Dictionary<int, decimal>>
    {
        { "Багажник(цена от)", new Dictionary<int, decimal> { { 1, 300 } } },
        { "Пылесос(цена от)", new Dictionary<int, decimal> { { 1, 300 } } },
        { "Влажная уборка(цена от)", new Dictionary<int, decimal> { { 1, 300 } } },
        { "Стекла(цена от)", new Dictionary<int, decimal> { { 1, 300 } } },
        { "Кварцевое покрытие", new Dictionary<int, decimal> { { 1, 850 }, { 2, 950 }, { 3, 1050 }, { 4, 1150 } } },
        { "Полироль пластика", new Dictionary<int, decimal> { { 1, 300 } } },
        { "Кондиционер кожи(цена от)", new Dictionary<int, decimal> { { 1, 1500 } } },
        { "Чистка руля", new Dictionary<int, decimal> { { 1, 500 } } },
        { "Обработка силиконом", new Dictionary<int, decimal> { { 1, 300 } } },
        { "Удаление насекомых", new Dictionary<int, decimal> { { 1, 250 } } },
        { "Битум, металлические вкрапления(цена за элемент)", new Dictionary<int, decimal> { { 1, 150 } } },
        { "Антидождь быстрый", new Dictionary<int, decimal> { { 1, 150 } } },
        { "Антидождь крайтека(передний контур)", new Dictionary<int, decimal> { { 1, 3500 } } },
        { "Антидождь крайтека(вкруг)", new Dictionary<int, decimal> { { 1, 6000 } } },
        { "Очистка дисков", new Dictionary<int, decimal> { { 1, 300 } } },
        { "Мойка колес", new Dictionary<int, decimal> { { 1, 1200 } } },
        { "Мойка двигателя(цена от)", new Dictionary<int, decimal> { { 1, 1500 } } }
    };

        // Используем GetNextServiceId() для каждого добавления
        foreach (var service in mainServices)
        {
            data.Services.Add(new Service
            {
                Id = data.GetNextServiceId(), // ← ИСПРАВЛЕНО
                Name = service.Key,
                DurationMinutes = GetDurationForService(service.Key),
                Description = GetDescriptionForService(service.Key),
                IsActive = true,
                PriceByBodyType = service.Value
            });
        }

        foreach (var service in extraServices)
        {
            data.Services.Add(new Service
            {
                Id = data.GetNextServiceId(), // ← ИСПРАВЛЕНО
                Name = service.Key,
                DurationMinutes = GetDurationForService(service.Key),
                Description = GetDescriptionForService(service.Key),
                IsActive = true,
                PriceByBodyType = service.Value
            });
        }

        System.Diagnostics.Debug.WriteLine($"Загружено услуг: {data.Services.Count}");
    }

    private static int GetDurationForService(string serviceName)
    {
        // Обычный switch вместо switch expression
        switch (serviceName)
        {
            case "Техническая мойка":
                return 30;
            case "Профессиональная мойка кузова":
                return 45;
            case "Комплекс \"ИЗИ\"":
                return 60;
            case "Комплекс \"Глянец\"":
                return 90;
            case "Кварцевое покрытие":
                return 60;
            case "Полироль пластика":
                return 30;
            case "Кондиционер кожи(цена от)":
                return 45;
            case "Чистка руля":
                return 15;
            case "Обработка силиконом":
                return 20;
            case "Удаление насекомых":
                return 15;
            case "Битум, металлические вкрапления(цена за элемент)":
                return 15;
            case "Антидождь быстрый":
                return 15;
            case "Антидождь крайтека(передний контур)":
                return 30;
            case "Антидождь крайтека(вкруг)":
                return 45;
            case "Очистка дисков":
                return 20;
            case "Мойка колес(под щетку 4 колеса с шампунем)":
                return 30;
            case "Мойка двигателя(цена от)":
                return 45;
            default:
                return 30;
        }
    }

    private static string GetDescriptionForService(string serviceName)
    {
        // Обычный switch вместо switch expression
        switch (serviceName)
        {
            case "Техническая мойка":
                return "Двухфазная мойка без сушки, коврики";
            case "Профессиональная мойка кузова":
                return "Двухфазная мойка, воск, турбосушка, коврики, педальный блок";
            case "Комплекс \"ИЗИ\"":
                return "Трехфазная мойка, влажная уборка, пылесос, стекла, турбосушка, коврики, педальный блок, чернение";
            case "Комплекс \"Глянец\"":
                return "Двухфазная мойка, кварц, влажная уборка, пылесос, стекла, багажник, полироль, турбосушка, коврики, педальный блок, чернение";
            case "Кварцевое покрытие":
                return "Защитное кварцевое покрытие кузова";
            case "Полироль пластика":
                return "Полировка пластиковых элементов";
            case "Кондиционер кожи(цена от)":
                return "Уход за кожаным салоном";
            case "Чистка руля":
                return "Химчистка рулевого колеса";
            case "Обработка силиконом":
                return "Силиконовая обработка уплотнительных резинок";
            case "Удаление насекомых":
                return "Удаление следов насекомых";
            case "Битум, металлические вкрапления(цена за элемент)":
                return "Удаление битумных пятен";
            case "Антидождь быстрый":
                return "Быстрое нанесение антидождя";
            case "Антидождь крайтека(передний контур)":
                return "Качественный антидождь на переднюю часть";
            case "Антидождь крайтека(вкруг)":
                return "Качественный антидождь на весь автомобиль";
            case "Очистка дисков":
                return "Очистка колесных дисков";
            case "Мойка колес":
                return "Полная мойка колес щеткой с шампунем";
            case "Мойка двигателя(цена от)":
                return "Мойка подкапотного пространства";
            default:
                return "";
        }
    }
}
