using AccuratSystem.Contracts.Models;
using AccuratPanelCarWashing.Services;
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
            await System.Threading.Tasks.Task.Delay(5000);

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

                // AuthenticateAsync возвращает ContractsUser
                var contractsUser = await _apiService.AuthenticateAsync(login, password);

                if (contractsUser != null)
                {
                    AppSettings.CurrentBranchId = selectedBranch.Id;
                    AppSettings.CurrentBranchName = selectedBranch.Name;
                    AppSettings.CurrentBranchWashBaysCount = selectedBranch.WashBaysCount;
                    AppSettings.CurrentBranchServiceLiftsCount = selectedBranch.ServiceLiftsCount;

                    Logger.SetUserContext(contractsUser.FullName, contractsUser.Id);

                    // === ИСПРАВЛЕНО: Создаём WpfUser на основе ContractsUser ===
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
                        // IsAdmin вычисляется автоматически на основе Role
                    };

                    //  НОВАЯ ЛОГИКА МАРШРУТИЗАЦИИ
                    if (wpfUser.Role == 1) // Директор
                    {
                        var directorWin = new MainWindow(wpfUser);
                        directorWin.Show();
                    }
                    else // Админ, Мойщик, Сотрудник сервиса
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