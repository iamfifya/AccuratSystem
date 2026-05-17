using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Polly;

namespace AccuratPanelCarWashing.Services // (Для MAUI будет AccuratPanelCWM.Services)
{
    public class HttpRetryHandler : DelegatingHandler
    {
        public HttpRetryHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Настраиваем логику: 3 попытки, пауза увеличивается (1 сек, 2 сек, 3 сек)
            var retryPolicy = Policy
                .Handle<HttpRequestException>() // Ловим отвалы интернета и недоступность сервера
                .Or<TaskCanceledException>()    // Ловим таймауты (если сервер задумался)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(retryAttempt),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    // Выводим в консоль для отладки
                    System.Diagnostics.Debug.WriteLine($"[POLLY] Сбой сети ({exception.Message}). Попытка {retryCount} через {timeSpan.TotalSeconds} сек...");
                });

            // Выполняем запрос через политику Polly
            return retryPolicy.ExecuteAsync(() => base.SendAsync(request, cancellationToken));
        }
    }
}