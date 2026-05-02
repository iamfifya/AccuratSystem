using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace AccuratPanelCarWashing
{
    public partial class LoginWindow : Window
    {
        private readonly ApiService _apiService = new ApiService();

        // 1. УБРАЛИ SqliteDataService ИЗ КОНСТРУКТОРА
        public LoginWindow()
        {
            InitializeComponent();

            // Загружаем филиалы при старте
            _ = LoadBranchesAsync();
        }

        private async System.Threading.Tasks.Task LoadBranchesAsync()
        {
            try
            {
                var branches = await _apiService.GetBranchesAsync();
                BranchComboBox.ItemsSource = branches;
                if (branches.Any()) BranchComboBox.SelectedItem = branches.FirstOrDefault();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось связаться с сервером для получения списка филиалов: {ex.Message}");
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginTextBox.Text.Trim();
            string password = PasswordBox.Password;
            var selectedBranch = BranchComboBox.SelectedItem as Branch;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password) || selectedBranch == null)
            {
                MessageBox.Show("Заполните все поля и выберите филиал!", "Внимание");
                return;
            }

            try
            {
                this.IsEnabled = false;

                // 1. Авторизация на сервере
                var user = await _apiService.AuthenticateAsync(login, password);

                if (user != null)
                {
                    // 2. Устанавливаем выбранный филиал в глобальные настройки приложения
                    AppSettings.CurrentBranchId = selectedBranch.Id;
                    AppSettings.CurrentBranchName = selectedBranch.Name;

                    Logger.SetUserContext(user.FullName, user.Id);
                    Logger.Info("Вход выполнен", "AUTH", $"Филиал: {selectedBranch.Name}");

                    // 3. ПЕРЕДАЕМ ТОЛЬКО USER В MAINWINDOW (без Sqlite)
                    var mainWindow = new MainWindow(user);
                    mainWindow.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Неверный логин или пароль!", "Ошибка");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка авторизации: {ex.Message}");
            }
            finally
            {
                this.IsEnabled = true;
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
    }
}
