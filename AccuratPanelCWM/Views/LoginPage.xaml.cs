using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using AccuratPanelCWM.Models;
using AccuratPanelCWM.Services;

namespace AccuratPanelCWM.Views
{
    public partial class LoginPage : ContentPage
    {
        private readonly ApiService _apiService;

        public LoginPage()
        {
            InitializeComponent();
            _apiService = new ApiService();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadBranchesAsync();
        }

        private async Task LoadBranchesAsync()
        {
            try
            {
                var branches = await _apiService.GetBranchesAsync();
                BranchPicker.ItemsSource = branches;

                if (branches.Any())
                {
                    BranchPicker.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка", $"Не удалось получить список филиалов: {ex.Message}", "ОК");
            }
        }

        private async void LoginButton_Clicked(object sender, EventArgs e)
        {
            // СПАСИТЕЛЬНЫЙ TRIM() ДЛЯ МОБИЛЬНЫХ КЛАВИАТУР
            string login = LoginEntry.Text?.Trim();
            string password = PasswordEntry.Text;
            var selectedBranch = BranchPicker.SelectedItem as Branch;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password) || selectedBranch == null)
            {
                await DisplayAlert("Внимание", "Заполните все поля и выберите филиал!", "ОК");
                return;
            }

            try
            {
                LoginButton.IsEnabled = false;

                var user = await _apiService.AuthenticateAsync(login, password);

                if (user != null)
                {
                    // Точно как в твоем WPF коде: сохраняем данные филиала глобально
                    AppSettings.CurrentBranchId = selectedBranch.Id;
                    AppSettings.CurrentBranchName = selectedBranch.Name;
                    AppSettings.CurrentBranchWashBaysCount = selectedBranch.WashBaysCount;
                    AppSettings.CurrentBranchServiceLiftsCount = selectedBranch.ServiceLiftsCount;

                    // Успешный вход -> Перекидываем на главную страницу с TabBar (AppShell)
                    Application.Current.MainPage = new AppShell();
                }
                else
                {
                    await DisplayAlert("Ошибка", "Неверный логин или пароль!", "ОК");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка сети", $"Сбой авторизации: {ex.Message}", "ОК");
            }
            finally
            {
                LoginButton.IsEnabled = true;
            }
        }
    }
}