using AccuratPanelCWM.Services;
using AccuratPanelCWM.Views;
using AccuratPanelCWM.ViewModels; // Не забудь прописать
using Microsoft.Extensions.Logging;
using LiveChartsCore.SkiaSharpView.Maui;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace AccuratPanelCWM
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseSkiaSharp()
                .UseLiveCharts()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // === 1. РЕГИСТРИРУЕМ ПЕРЕХВАТЧИК (Для X-Company-Id) ===
            builder.Services.AddTransient<TenantAuthHandler>();

            // === 2. РЕГИСТРИРУЕМ HTTP CLIENT ===
            builder.Services.AddHttpClient("ApiClient", client =>
            {
                var url = Microsoft.Maui.Storage.Preferences.Default.Get("ServerUrl", "https://192qb7z7-7165.euw.devtunnels.ms/api/");
                client.BaseAddress = new Uri(url);
                client.DefaultRequestHeaders.Add("X-Tunnel-Skip-AntiPhishing-Page", "true");
            })
            .AddHttpMessageHandler<TenantAuthHandler>();

            // === 3. РЕГИСТРИРУЕМ СЕРВИСЫ ===
            builder.Services.AddSingleton<ApiService>();

            // === 4. РЕГИСТРИРУЕМ VIEWS И VIEWMODELS ===
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<LoginPage>();

            builder.Services.AddTransient<ManagementViewModel>();
            builder.Services.AddTransient<ManagementPage>();

            builder.Services.AddTransient<OrdersViewModel>();
            builder.Services.AddTransient<OrdersPage>();

            builder.Services.AddTransient<AddOrderViewModel>();
            builder.Services.AddTransient<AddOrderPage>();

            builder.Services.AddTransient<ReportsViewModel>();
            builder.Services.AddTransient<ReportsPage>();

            builder.Services.AddTransient<CashboxViewModel>();
            builder.Services.AddTransient<CashboxPage>();

            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddTransient<SettingsPage>();

            return builder.Build();
        }
    }
}