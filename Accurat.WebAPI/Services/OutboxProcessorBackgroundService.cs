using Accurat.WebAPI.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Accurat.WebAPI.Services
{
    public class OutboxProcessorBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OutboxProcessorBackgroundService> _logger;

        public OutboxProcessorBackgroundService(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessorBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Фоновый обработчик Outbox запущен.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOutboxMessagesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Критическая ошибка при обработке Outbox-сообщений.");
                }

                // Ждем 10 секунд перед следующей проверкой (чтобы не спамить базу)
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        private async Task ProcessOutboxMessagesAsync(CancellationToken stoppingToken)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Теперь берем только те, которые еще не выполнены И у которых меньше 5 попыток
                var messages = context.OutboxMessages
                    .Where(m => m.ProcessedAtUtc == null && m.RetryCount < 5)
                    .OrderBy(m => m.CreatedAtUtc)
                    .Take(20)
                    .ToList();

                if (!messages.Any())
                {
                    return; // Нет задач - идем спать
                }

                foreach (var message in messages)
                {
                    try
                    {
                        // Распределяем задачи по их типу
                        switch (message.EventType)
                        {
                            case "OrderCompleted":
                                _logger.LogInformation($"Обработка события OrderCompleted (ID: {message.Id}). Данные: {message.PayloadJson}");
                                // В БУДУЩЕМ: Здесь мы будем обращаться к SMS-шлюзу или пересчитывать бонусы
                                // await _smsService.SendAsync(...)
                                break;

                            default:
                                _logger.LogWarning($"Неизвестный тип события: {message.EventType}");
                                break;
                        }

                        // Помечаем задачу как успешно выполненную
                        message.ProcessedAtUtc = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        // 1. Увеличиваем счетчик попыток
                        message.RetryCount++;

                        // 2. Записываем ошибку
                        message.ErrorMessage = ex.Message;

                        // 3. Если попыток стало 5, логируем, что задача официально «мертва»
                        if (message.RetryCount >= 5)
                        {
                            _logger.LogError($"[OUTBOX] Задача {message.Id} признана невыполнимой после 5 попыток. Ошибка: {ex.Message}");
                        }
                        else
                        {
                            _logger.LogWarning($"[OUTBOX] Ошибка при выполнении задачи {message.Id}. Попытка {message.RetryCount}/5. Будет повторена через 10 сек.");
                        }
                    }

                }

                // Сохраняем изменения в статусах задач
                await context.SaveChangesAsync(stoppingToken);
            }
        }
    }
}