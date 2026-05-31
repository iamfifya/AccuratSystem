using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using AccuratSystem.Contracts.DTOs;
using AccuratSystem.Contracts.Models;
using System;
using System.Linq;
using System.Windows;
using WpfUser = AccuratPanelCarWashing.Models.User;

namespace AccuratPanelCarWashing
{
    public partial class LoginWindow : Window
    {
        private readonly ApiService _apiService = new ApiService();
        private LoginResponseDto _loginResponse;
        private bool _isStepTwo = false;

        public WpfUser AuthenticatedUser { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
        }

        private async void ActionBtn_Click(object sender, RoutedEventArgs e)
        {
            // ШАГ 2: Выбор филиала
            if (_isStepTwo)
            {
                if (BranchComboBox.SelectedValue is int branchId)
                {
                    var selectedBranch = _loginResponse.AvailableBranches.FirstOrDefault(b => b.Id == branchId);
                    if (selectedBranch != null) CompleteLogin(selectedBranch);
                }
                else
                {
                    MessageBox.Show("Пожалуйста, выберите филиал из списка.", "Внимание");
                }
                return;
            }

            // ШАГ 1: Ввод логина и пароля
            string login = LoginTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Введите логин и пароль!", "Внимание");
                return;
            }

            try
            {
                this.IsEnabled = false;

                _loginResponse = await _apiService.AuthenticateAsync(login, password);

                if (_loginResponse != null && _loginResponse.User != null)
                {
                    var branches = _loginResponse.AvailableBranches;

                    if (branches == null || !branches.Any())
                    {
                        MessageBox.Show("У вас нет доступа ни к одному филиалу. Обратитесь к администратору.", "Доступ закрыт");
                        return;
                    }

                    if (branches.Count == 1)
                    {
                        // Если филиал один — пускаем сразу
                        CompleteLogin(branches.First());
                    }
                    else
                    {
                        // Если несколько — переключаем интерфейс на ШАГ 2
                        LoginPanel.Visibility = Visibility.Collapsed;
                        BranchSelectionPanel.Visibility = Visibility.Visible;

                        BranchComboBox.ItemsSource = branches;

                        // Выбираем первый элемент вручную
                        BranchComboBox.SelectedValue = branches.First().Id;

                        ActionBtn.Content = "Продолжить";
                        _isStepTwo = true;
                    }
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

        private void CompleteLogin(Branch selectedBranch)
        {
            if (_loginResponse.Features != null)
            {
                AccuratPanelCarWashing.Services.UserSession.Features = _loginResponse.Features;
            }

            AppSettings.CurrentBranchId = selectedBranch.Id;
            AppSettings.CurrentBranchName = selectedBranch.Name;
            AppSettings.CurrentBranchWashBaysCount = selectedBranch.WashBaysCount;
            AppSettings.CurrentBranchServiceLiftsCount = selectedBranch.ServiceLiftsCount;

            var contractsUser = _loginResponse.User;
            Logger.SetUserContext(contractsUser.FullName, contractsUser.Id);

            AuthenticatedUser = new WpfUser
            {
                Id = contractsUser.Id,
                FullName = contractsUser.FullName,
                Phone = contractsUser.Phone,
                Login = contractsUser.Login,
                PasswordHash = contractsUser.PasswordHash,
                RoleId = contractsUser.RoleId,
                Role = contractsUser.Role,
                IsActive = contractsUser.IsActive,
                BranchId = selectedBranch.Id,
                CompanyId = contractsUser.CompanyId,
                BaseWagePercentage = contractsUser.BaseWagePercentage
            };

            // Передаем ID компании в HttpClient, чтобы все последующие запросы были изолированы
            // Если CompanyId = null (мы Разработчик), отправляем 0 как ключ Режима Бога
            int tenantContextId = contractsUser.CompanyId ?? 0;
            _apiService.UpdateTenantContext(tenantContextId);

            this.DialogResult = true;
            this.Close();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
    }
}