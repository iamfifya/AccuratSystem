using Microsoft.Extensions.DependencyInjection;
using AccuratPanelCarWashing.Controls;
using AccuratPanelCarWashing.Services;
using AccuratPanelCarWashing.ViewModels;
using System;
using System.Windows;
using AccuratPanelCarWashing.Models;

namespace AccuratPanelCarWashing
{
    public partial class App : Application
    {
        public static User CurrentUser { get; set; }
        private ServiceProvider _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Глобальная обработка необработанных исключений
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                MessageBox.Show($"Критическая ошибка: {ex?.Message}\n\nПриложение будет закрыто.",
                    "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка UI: {args.Exception.Message}");
                System.Diagnostics.Debug.WriteLine(args.Exception.StackTrace);
                args.Handled = true;
            };

            // Настройка DI
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Запуск приложения
            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            loginWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {

            // Регистрируем ViewModels как Transient (новый экземпляр при каждом запросе)
            services.AddTransient<AddEditOrderViewModel>();

            // Регистрируем окна как Transient
            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
            services.AddTransient<AddEditOrderWindow>();
            services.AddTransient<EmployeeCardWindow>();
            services.AddTransient<AddEditEmployeeWindow>();
            services.AddTransient<ServiceManagementWindow>();
            services.AddTransient<AddEditServiceWindow>();
            services.AddTransient<ReportsWindow>();
            services.AddTransient<CustomReportWindow>();
            services.AddTransient<StartShiftWindow>();
            services.AddTransient<ClientsWindow>();
            services.AddTransient<AddEditClientWindow>();
            services.AddTransient<ScheduleWindow>();
            services.AddTransient<AppointmentsOverlay>();
            services.AddTransient<WasherSelectionDialog>();
        }

        public static T GetService<T>() where T : class
        {
            if (Current is App app && app._serviceProvider != null)
                return app._serviceProvider.GetRequiredService<T>();

            throw new InvalidOperationException("ServiceProvider is not initialized");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }
}
