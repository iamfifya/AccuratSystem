using AccuratPanelCWM.Services;

namespace AccuratPanelCWM.Views;

public partial class SettingsPage : ContentPage
{
    private readonly ApiService _apiService;
    private bool _isInitializing; // Предохранитель от ложных срабатываний

    public SettingsPage()
    {
        InitializeComponent();
        _apiService = new ApiService();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _isInitializing = true;

        ServerUrlEntry.Text = Preferences.Default.Get("ServerUrl", "https://192qb7z7-7165.euw.devtunnels.ms/api/");

        // Извлекаем сохраненную строку темы оформления
        string savedTheme = Preferences.Default.Get("AppTheme", "System");

        // Привязываем строку из памяти к индексу Picker
        switch (savedTheme)
        {
            case "System":
                ThemePicker.SelectedIndex = 0;
                break;
            case "Light":
                ThemePicker.SelectedIndex = 1;
                break;
            case "Dark":
                ThemePicker.SelectedIndex = 2;
                break;
        }

        _isInitializing = false;
    }

    private void ThemePicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_isInitializing || ThemePicker.SelectedIndex == -1) return;

        switch (ThemePicker.SelectedIndex)
        {
            case 0: // Как в системе
                Application.Current.UserAppTheme = AppTheme.Unspecified;
                Preferences.Default.Set("AppTheme", "System");
                break;

            case 1: // Светлая
                Application.Current.UserAppTheme = AppTheme.Light;
                Preferences.Default.Set("AppTheme", "Light");
                break;

            case 2: // Ночная
                Application.Current.UserAppTheme = AppTheme.Dark;
                Preferences.Default.Set("AppTheme", "Dark");
                break;
        }
    }

    private async void SaveUrlButton_Clicked(object sender, EventArgs e)
    {
        string newUrl = ServerUrlEntry.Text?.Trim();
        if (string.IsNullOrEmpty(newUrl) || !newUrl.StartsWith("http"))
        {
            await DisplayAlert("Ошибка", "Введите корректный URL (http:// или https://)", "ОК");
            return;
        }

        try
        {
            _apiService.UpdateBaseUrl(newUrl);
            await DisplayAlert("Успешно", "Новый адрес сервера применен!", "ОК");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", $"Не удалось обновить адрес: {ex.Message}", "ОК");
        }
    }

    private async void LogoutButton_Clicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Выход", "Вы действительно хотите выйти из аккаунта?", "Да", "Отмена");
        if (confirm)
        {
            Preferences.Default.Remove("SavedLogin");
            Preferences.Default.Remove("SavedPassword");
            Application.Current.MainPage = new NavigationPage(new LoginPage());
        }
    }
}