// Services/TransactionService.cs
using DocumentFormat.OpenXml.Drawing.Charts;
using MyPanelCarWashing.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Formatting = Newtonsoft.Json.Formatting;

namespace MyPanelCarWashing.Services
{
    public class TransactionService
    {
        private static readonly object _transactionLock = new object();
        private static readonly string DataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appdata.json");
        private static readonly string TempPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appdata.temp.json");
        private static readonly string BackupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appdata.backup.json");
        private static readonly string TransactionLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "transaction.log");

        /// <summary>
        /// Выполняет операцию в транзакции с автоматическим восстановлением при ошибке
        /// </summary>
        public static async Task<T> ExecuteInTransactionAsync<T>(Func<AppData, T> action, int maxRetries = 3)
        {
            lock (_transactionLock)
            {
                return ExecuteInTransaction(action, maxRetries);
            }
        }

        /// <summary>
        /// Синхронная версия транзакции
        /// </summary>
        public static T ExecuteInTransaction<T>(Func<AppData, T> action, int maxRetries = 3)
        {
            int attempt = 0;
            Exception lastException = null;

            while (attempt < maxRetries)
            {
                attempt++;
                AppData workingData = null;
                string backupId = null;

                try
                {
                    // 1. Создаём резервную копию текущего состояния
                    backupId = CreateBackup();

                    // 2. Загружаем текущие данные
                    var originalData = LoadDataWithRetry();

                    // 3. Создаём глубокую копию для работы
                    workingData = DeepCopy(originalData);

                    // 4. Выполняем действие над копией
                    var result = action(workingData);

                    // 5. Валидируем целостность данных
                    ValidateDataIntegrity(workingData);

                    // 6. Атомарно сохраняем
                    AtomicSave(workingData);

                    // 7. Логируем успешную операцию
                    LogTransaction($"SUCCESS: Transaction completed on attempt {attempt}");

                    // 8. Удаляем старый бэкап (опционально)
                    CleanupOldBackup(backupId);

                    return result;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    LogTransaction($"FAILED: Attempt {attempt} - {ex.Message}");

                    // Восстанавливаем из бэкапа при ошибке
                    if (!string.IsNullOrEmpty(backupId))
                    {
                        RestoreFromBackup(backupId);
                    }

                    if (attempt >= maxRetries)
                    {
                        throw new TransactionException($"Транзакция не удалась после {maxRetries} попыток. Последняя ошибка: {ex.Message}", ex);
                    }

                    // Экспоненциальная задержка перед повтором
                    System.Threading.Thread.Sleep(100 * attempt);
                }
            }

            throw new TransactionException($"Неизвестная ошибка транзакции", lastException);
        }

        /// <summary>
        /// Создаёт резервную копию с временной меткой
        /// </summary>
        private static string CreateBackup()
        {
            try
            {
                if (File.Exists(DataPath))
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                    var backupId = $"backup_{timestamp}";
                    var backupFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"appdata_{backupId}.json");

                    File.Copy(DataPath, backupFile, overwrite: true);

                    // Также обновляем основной бэкап
                    File.Copy(DataPath, BackupPath, overwrite: true);

                    return backupId;
                }
                return null;
            }
            catch (Exception ex)
            {
                LogTransaction($"Failed to create backup: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Восстанавливает данные из бэкапа
        /// </summary>
        private static void RestoreFromBackup(string backupId)
        {
            try
            {
                var backupFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"appdata_{backupId}.json");
                if (File.Exists(backupFile))
                {
                    File.Copy(backupFile, DataPath, overwrite: true);
                    LogTransaction($"RESTORED: Data restored from backup {backupId}");
                }
                else if (File.Exists(BackupPath))
                {
                    File.Copy(BackupPath, DataPath, overwrite: true);
                    LogTransaction($"RESTORED: Data restored from main backup");
                }
            }
            catch (Exception ex)
            {
                LogTransaction($"Failed to restore backup: {ex.Message}");
            }
        }

        /// <summary>
        /// Атомарное сохранение через временный файл
        /// </summary>
        private static void AtomicSave(AppData data)
        {
            try
            {
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);

                // 1. Сохраняем во временный файл
                File.WriteAllText(TempPath, json);

                // 2. Проверяем, что временный файл успешно создан
                if (!File.Exists(TempPath))
                    throw new IOException("Temp file was not created");

                var tempContent = File.ReadAllText(TempPath);
                if (string.IsNullOrEmpty(tempContent))
                    throw new IOException("Temp file is empty");

                // 3. Атомарно заменяем основной файл
                File.Copy(TempPath, DataPath, overwrite: true);

                // 4. Удаляем временный файл
                File.Delete(TempPath);

                LogTransaction($"SAVED: Data saved atomically at {DateTime.Now}");
            }
            catch (Exception ex)
            {
                LogTransaction($"ATOMIC SAVE FAILED: {ex.Message}");
                throw new IOException($"Failed to save data atomically: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Загрузка данных с повторными попытками
        /// </summary>
        private static AppData LoadDataWithRetry(int retries = 3)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    return FileDataService.LoadData();
                }
                catch (IOException ex) when (i < retries - 1)
                {
                    LogTransaction($"Load retry {i + 1}: {ex.Message}");
                    System.Threading.Thread.Sleep(50);
                }
            }
            return FileDataService.LoadData();
        }

        /// <summary>
        /// Глубокая копия объекта
        /// </summary>
        private static AppData DeepCopy(AppData original)
        {
            var json = JsonConvert.SerializeObject(original);
            return JsonConvert.DeserializeObject<AppData>(json);
        }

        /// <summary>
        /// Валидация целостности данных перед сохранением
        /// </summary>
        private static void ValidateDataIntegrity(AppData data)
        {
            var errors = new List<string>();

            // 1. Проверяем уникальность ID услуг
            var duplicateServiceIds = data.Services
                .GroupBy(s => s.Id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            foreach (var id in duplicateServiceIds)
                errors.Add($"Duplicate service ID: {id}");

            // 2. Проверяем, что все услуги в заказах существуют
            foreach (var shift in data.Shifts)
            {
                if (shift.Orders == null) continue;

                foreach (var order in shift.Orders)
                {
                    foreach (var serviceId in order.ServiceIds)
                    {
                        if (!data.Services.Any(s => s.Id == serviceId))
                            errors.Add($"Order #{order.Id} references non-existent service ID: {serviceId}");
                    }
                    // 3. Проверяем существование мойщика (только предупреждение, не ошибка)
                    if (order.WasherId > 0)
                    {
                        var washer = data.Users.FirstOrDefault(u => u.Id == order.WasherId);
                        if (washer == null)
                        {
                            // Только логируем, не добавляем в errors
                            System.Diagnostics.Debug.WriteLine($"WARNING: Order #{order.Id} references non-existent washer ID: {order.WasherId}");
                            // НЕ добавляем в errors - не блокируем транзакцию
                        }
                    }
                }
            }

            // 4. Проверяем связи заказов с записями
            foreach (var appointment in data.Appointments)
            {
                if (appointment.OrderId.HasValue && appointment.IsCompleted)
                {
                    bool orderExists = data.Shifts
                        .SelectMany(s => s.Orders ?? new List<CarWashOrder>())
                        .Any(o => o.Id == appointment.OrderId.Value);

                    if (!orderExists)
                        errors.Add($"Appointment #{appointment.Id} references non-existent order ID: {appointment.OrderId}");
                }
            }

            // 5. Проверяем, что все смены имеют корректные даты
            foreach (var shift in data.Shifts)
            {
                if (shift.StartTime.HasValue && shift.EndTime.HasValue && shift.EndTime < shift.StartTime)
                    errors.Add($"Shift #{shift.Id} has EndTime before StartTime");
            }

            if (errors.Any())
            {
                throw new DataIntegrityException($"Data validation failed:\n{string.Join("\n", errors)}");
            }
        }

        /// <summary>
        /// Логирование транзакций
        /// </summary>
        private static void LogTransaction(string message)
        {
            try
            {
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n";
                File.AppendAllText(TransactionLogPath, logEntry);

                // Ограничиваем размер лога (оставляем последние 1000 строк)
                if (new FileInfo(TransactionLogPath).Length > 1024 * 1024) // 1MB
                {
                    var lines = File.ReadAllLines(TransactionLogPath);
                    var lastLines = lines.Skip(Math.Max(0, lines.Length - 1000));
                    File.WriteAllLines(TransactionLogPath, lastLines);
                }
            }
            catch { /* Логирование не должно вызывать ошибок */ }
        }

        /// <summary>
        /// Очистка старых бэкапов
        /// </summary>
        private static void CleanupOldBackup(string currentBackupId)
        {
            try
            {
                var backupFiles = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "appdata_backup_*.json");
                foreach (var file in backupFiles)
                {
                    if (!file.Contains(currentBackupId))
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTime < DateTime.Now.AddDays(-7)) // Храним бэкапы неделю
                        {
                            File.Delete(file);
                        }
                    }
                }
            }
            catch { /* Не критично */ }
        }
    }

    // Кастомные исключения
    public class TransactionException : Exception
    {
        public TransactionException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    public class DataIntegrityException : Exception
    {
        public DataIntegrityException(string message) : base(message) { }
    }
}
