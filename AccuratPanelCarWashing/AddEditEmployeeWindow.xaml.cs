using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

// !!! ДОБАВЛЯЕМ АЛИАСЫ, ЧТОБЫ УБРАТЬ ОШИБКУ CS0104 !!!
using ContractsUser = AccuratSystem.Contracts.Models.User;
using WpfUser = AccuratPanelCarWashing.Models.User;

namespace AccuratPanelCarWashing
{
    public partial class AddEditEmployeeWindow : Window
    {
        private readonly ApiService _apiService;

        // ИСПОЛЬЗУЕМ ContractsUser, так как эти данные уходят в API
        public ContractsUser CurrentEmployee { get; set; }
        public new string Title { get; set; }

        // Конструктор теперь принимает контрактного пользователя
        public AddEditEmployeeWindow(ContractsUser employee)
        {
            InitializeComponent();
            _apiService = new ApiService();

            // 💥 УБРАЛИ ХАРДКОД! Теперь список пустой, пока не скачается из БД
            // RoleComboBox.DisplayMemberPath = "Name";
            // RoleComboBox.SelectedValuePath = "Id";

            if (employee == null)
            {
                CurrentEmployee = new ContractsUser { IsActive = true, RoleId = 3, BranchId = AppSettings.CurrentBranchId };
                Title = "➕ Добавление сотрудника";
            }
            else
            {
                // Твой текущий код копирования сотрудника...
                CurrentEmployee = employee;
                Title = "✏ Редактирование сотрудника";
            }

            DataContext = this;

            // Запускаем асинхронную загрузку филиалов И ролей из БД
            _ = LoadBranchesAndRolesAsync();
        }

        private async Task LoadBranchesAndRolesAsync()
        {
            try
            {
                var branches = await _apiService.GetBranchesAsync();
                if (branches != null && branches.Any())
                {
                    BranchComboBox.ItemsSource = branches;
                    BranchComboBox.SelectedValue = CurrentEmployee.BranchId;
                }

                // 💥 КАЧАЕМ РОЛИ НАПРЯМУЮ ИЗ БАЗЫ:
                var roles = await _apiService.GetRolesAsync();
                RoleComboBox.ItemsSource = roles;
                RoleComboBox.SelectedValue = CurrentEmployee.RoleId;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки справочников: {ex.Message}", "Ошибка API");
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CurrentEmployee.FullName) || string.IsNullOrWhiteSpace(CurrentEmployee.Login))
                {
                    MessageBox.Show("Заполните ФИО и Логин", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (CurrentEmployee.BranchId == 0)
                {
                    MessageBox.Show("Пожалуйста, выберите филиал привязки сотрудника", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string password = PasswordBox.Password;
                if (CurrentEmployee.Id == 0 && string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Введите пароль для нового сотрудника", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(password)) CurrentEmployee.PasswordHash = password;

                this.IsEnabled = false;

                if (CurrentEmployee.Id == 0)
                {
                    await _apiService.CreateUserAsync(CurrentEmployee);
                    MessageBox.Show("Сотрудник добавлен в PostgreSQL", "Успешно");
                }
                else
                {
                    await _apiService.UpdateUserAsync(CurrentEmployee);
                    MessageBox.Show("Данные обновлены на сервере", "Успешно");
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка API: {ex.Message}", "Ошибка сохранения");
            }
            finally
            {
                this.IsEnabled = true;
            }
        }

        private void PhoneTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;
            string digits = new string(tb.Text.Where(char.IsDigit).ToArray());
            if (digits.Length == 0) return;
            string formatted = "8";
            if (digits.Length > 1) formatted += " (" + digits.Substring(1, Math.Min(3, digits.Length - 1));
            if (digits.Length > 4) formatted += ") " + digits.Substring(4, Math.Min(3, digits.Length - 4));
            if (digits.Length > 7) formatted += "-" + digits.Substring(7, Math.Min(2, digits.Length - 7));
            if (digits.Length > 9) formatted += "-" + digits.Substring(9, Math.Min(2, digits.Length - 9));
            if (tb.Text != formatted) { tb.Text = formatted; tb.CaretIndex = tb.Text.Length; }
        }

        private void PhoneTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e) =>
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, @"^[\d\+\s\-\(\)]+$");

        private void CloseButton_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
