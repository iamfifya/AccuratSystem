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
            // Ждем 2 секунды, чтобы Web API успел запустить свои службы
            await System.Threading.Tasks.Task.Delay(5000);

            try
            {
                var branches = await _apiService.GetBranchesAsync();

                // НОВАЯ ПРОВЕРКА: Если сервер вернул пустоту, мы об этом узнаем!
                if (branches == null || !branches.Any())
                {
                    MessageBox.Show("Список филиалов пуст! Проверьте подключение к API и наличие филиалов в базе.", "Отладка");
                    return;
                }

                BranchComboBox.ItemsSource = branches;
                BranchComboBox.SelectedItem = branches.FirstOrDefault();
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
                var user = await _apiService.AuthenticateAsync(login, password);

                if (user != null)
                {
                    AppSettings.CurrentBranchId = selectedBranch.Id;
                    AppSettings.CurrentBranchName = selectedBranch.Name;
                    AppSettings.CurrentBranchWashBaysCount = selectedBranch.WashBaysCount;
                    AppSettings.CurrentBranchServiceLiftsCount = selectedBranch.ServiceLiftsCount;

                    Logger.SetUserContext(user.FullName, user.Id);

                    //  НОВАЯ ЛОГИКА МАРШРУТИЗАЦИИ
                    if (user.Role == 1) // Директор
                    {
                        var directorWin = new MainWindow(user);
                        directorWin.Show();
                    }
                    else // Админ, Мойщик, Сотрудник сервиса
                    {
                        var mainWindow = new MainWindow(user);
                        mainWindow.Show();
                    }
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
