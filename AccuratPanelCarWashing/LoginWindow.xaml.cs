using AccuratSystem.Contracts.Models;
using AccuratPanelCarWashing.Services;
using AccuratSystem.Contracts.DTOs; // Добавлено для LoginResponseDto
using System;
using System.Linq;
using System.Windows;
using AccuratPanelCarWashing.Models;

namespace AccuratPanelCarWashing
{
    public partial class LoginWindow : Window
    {
        private readonly ApiService _apiService = new ApiService();

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
                    // === БЛОК ДИАГНОСТИКИ (УДАЛИМ ПОТОМ) ===
                    string debugInfo = $"Филиал: {selectedBranch.Id}\n" +
                                       $"Склад: {loginResponse.Features?.IsStorageEnabled}\n" +
                                       $"CRM: {loginResponse.Features?.IsCrmMarketingEnabled}\n" +
                                       $"Upsell: {loginResponse.Features?.IsUpsellEnabled}\n" +
                                       $"Boss: {loginResponse.Features?.IsTelegramBossEnabled}";

                    MessageBox.Show(debugInfo, "ПРОВЕРКА ЛИЦЕНЗИЙ");
                    // ========================================

                    AccuratPanelCarWashing.Services.UserSession.Features = loginResponse.Features;

                    AppSettings.CurrentBranchId = selectedBranch.Id;
                    AppSettings.CurrentBranchName = selectedBranch.Name;
                    AppSettings.CurrentBranchWashBaysCount = selectedBranch.WashBaysCount;
                    AppSettings.CurrentBranchServiceLiftsCount = selectedBranch.ServiceLiftsCount;

                    var contractsUser = loginResponse.User;
                    Logger.SetUserContext(contractsUser.FullName, contractsUser.Id);

                    var wpfUser = new AccuratPanelCarWashing.Models.User
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

                    if (wpfUser.Role == 1)
                    {
                        var directorWin = new MainWindow(wpfUser);
                        directorWin.Show();
                    }
                    else
                    {
                        var mainWindow = new MainWindow(wpfUser);
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
