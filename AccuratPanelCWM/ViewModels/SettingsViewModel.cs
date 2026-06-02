using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AccuratPanelCWM.Services;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Controls;

namespace AccuratPanelCWM.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ApiService _apiService;
        private readonly IServiceProvider _serviceProvider;
        private bool _isInitializing;

        [ObservableProperty] private string _serverUrl;
        [ObservableProperty] private int _selectedThemeIndex = -1;

        public SettingsViewModel(ApiService apiService, IServiceProvider serviceProvider)
        {
            _apiService = apiService;
            _serviceProvider = serviceProvider;

            InitializeSettings();
        }

        private void InitializeSettings()
        {
            _isInitializing = true;

            // Загружаем текущие конфигурации из постоянной памяти устройства
            ServerUrl = Preferences.Default.Get("ServerUrl", "https://192qb7z7-7165.euw.devtunnels.ms/api/");
            string savedTheme = Preferences.Default.Get("AppTheme", "System");

            SelectedThemeIndex = savedTheme switch
            {
                "System" => 0,
                "Light" => 1,
                "Dark" => 2,
                _ => 0
            };

            _isInitializing = false;
        }

        // Автоматически срабатывает при выборе новой темы в Picker
        partial void OnSelectedThemeIndexChanged(int value)
        {
            if (_isInitializing || value == -1) return;

            switch (value)
            {
                case 0:
                    Application.Current.UserAppTheme = AppTheme.Unspecified;
                    Preferences.Default.Set("AppTheme", "System");
                    break;
                case 1:
                    Application.Current.UserAppTheme = AppTheme.Light;
                    Preferences.Default.Set("AppTheme", "Light");
                    break;
                case 2:
                    Application.Current.UserAppTheme = AppTheme.Dark;
                    Preferences.Default.Set("AppTheme", "Dark");
                    break;
            }
        }

        [RelayCommand]
        private async Task SaveUrlAsync()
        {
            string newUrl = ServerUrl?.Trim();
            if (string.IsNullOrEmpty(newUrl) || !newUrl.StartsWith("http"))
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", "Введите корректный URL (http:// или https://)", "ОК");
                return;
            }

            try
            {
                _apiService.UpdateBaseUrl(newUrl);
                await Application.Current.MainPage.DisplayAlert("Успешно", "Новый адрес сервера применен!", "ОК");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", $"Не удалось обновить адрес: {ex.Message}", "ОК");
            }
        }

        [RelayCommand]
        private async Task LogoutAsync()
        {
            bool confirm = await Application.Current.MainPage.DisplayAlert("Выход", "Вы действительно хотите выйти из аккаунта?", "Да", "Отмена");
            if (confirm)
            {
                // Зачищаем авторизационные токены и сессию
                Preferences.Default.Remove("SavedLogin");
                Preferences.Default.Remove("SavedPassword");
                Preferences.Default.Remove("CompanyId");
                Preferences.Default.Remove("CurrentBranchId");

                // Запрашиваем чистый экран LoginPage через DI-контейнер платформы
                var loginPage = _serviceProvider.GetRequiredService<Views.LoginPage>();
                Application.Current.MainPage = loginPage;
            }
        }
    }
}