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

                // Берем 20 самых старых невыполненных задач
                var messages = context.OutboxMessages
                    .Where(m => m.ProcessedAtUtc == null)
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
                        // Если SMS не отправилась, записываем ошибку, но не крашим весь цикл
                        message.ErrorMessage = ex.Message;
                        _logger.LogError(ex, $"Ошибка при выполнении задачи {message.Id}");
                    }
                }

                // Сохраняем изменения в статусах задач
                await context.SaveChangesAsync(stoppingToken);
            }
        }
    }
}