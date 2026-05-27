using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using AccuratSystem.Contracts.DTOs; // Добавлено для LoginResponseDto
using AccuratSystem.Contracts.Models;
using Microsoft.VisualBasic.ApplicationServices;
using System;
using System.Linq;
using System.Windows;

using WpfUser = AccuratPanelCarWashing.Models.User;

namespace AccuratPanelCarWashing
{
    public partial class LoginWindow : Window
    {
        private readonly ApiService _apiService = new ApiService();
        public WpfUser AuthenticatedUser { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
            _ = LoadBranchesAsync();
        }

        private async System.Threading.Tasks.Task LoadBranchesAsync()
        {
            // Небольшая задержка, чтобы интерфейс успел отрисоваться
            await System.Threading.Tasks.Task.Delay(1000);

            try
            {
                var branches = await _apiService.GetBranchesAsync();

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
                var loginResponse = await _apiService.AuthenticateAsync(login, password, selectedBranch.Id);

                if (loginResponse != null && loginResponse.User != null)
                {
                    // Сохраняем лицензии в сессию
                    AccuratPanelCarWashing.Services.UserSession.Features = loginResponse.Features;

                    // Сохраняем данные филиала
                    AppSettings.CurrentBranchId = selectedBranch.Id;
                    AppSettings.CurrentBranchName = selectedBranch.Name;
                    AppSettings.CurrentBranchWashBaysCount = selectedBranch.WashBaysCount;
                    AppSettings.CurrentBranchServiceLiftsCount = selectedBranch.ServiceLiftsCount;

                    var contractsUser = loginResponse.User;
                    Logger.SetUserContext(contractsUser.FullName, contractsUser.Id);

                    // Создаем UI-модель пользователя
                    AuthenticatedUser = new WpfUser
                    {
                        Id = contractsUser.Id,
                        FullName = contractsUser.FullName,
                        Phone = contractsUser.Phone,
                        Login = contractsUser.Login,
                        PasswordHash = contractsUser.PasswordHash,
                        Role = contractsUser.Role,
                        IsActive = contractsUser.IsActive,
                        BranchId = contractsUser.BranchId,
                        BaseWagePercentage = contractsUser.BaseWagePercentage
                    };

                    // ВАЖНО: Вместо того чтобы самому открывать MainWindow, 
                    // мы просто говорим, что авторизация прошла успешно.
                    this.DialogResult = true;
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
