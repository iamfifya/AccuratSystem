using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using AccuratSystem.Contracts.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using WpfUser = AccuratPanelCarWashing.Models.User;
using ContractsUser = AccuratSystem.Contracts.Models.User;

namespace AccuratPanelCarWashing
{
    public partial class EmployeeCardWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private ApiService _apiService;

        private List<WpfUser> _allEmployees;
        private List<WpfUser> _employeesList;
        private string _searchFilter = "";
        private WpfUser _selectedEmployee;

        public List<WpfUser> EmployeesList
        {
            get => _employeesList;
            set { _employeesList = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EmployeesList))); }
        }

        public EmployeeCardWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            DataContext = this;
            _ = LoadEmployeesAsync();
        }

        private async Task LoadEmployeesAsync()
        {
            try
            {
                var usersFromApi = await _apiService.GetUsersAsync();
                _allEmployees = usersFromApi.Select(u => new WpfUser
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Login = u.Login,
                    PasswordHash = u.PasswordHash,           // ← ДОБАВИТЬ
                    Role = u.Role,
                    IsActive = u.IsActive,                   // ← ДОБАВИТЬ
                    Phone = u.Phone,
                    BranchId = u.BranchId,
                    BaseWagePercentage = u.BaseWagePercentage,   // ← ДОБАВИТЬ
                    BaseSalaryPerShift = u.BaseSalaryPerShift    // ← ДОБАВИТЬ
                }).ToList();

                await Dispatcher.InvokeAsync(() => ApplyFilter());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки сотрудников: {ex.Message}", "Ошибка API");
            }
        }

        private void ApplyFilter()
        {
            if (_allEmployees == null) return;
            var filtered = _allEmployees.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(_searchFilter))
            {
                filtered = filtered.Where(e =>
                    e.FullName.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.Login.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            filtered = filtered.OrderBy(e => e.Role).ThenBy(e => e.FullName);

            EmployeesList = filtered.ToList();
            EmployeesListView.ItemsSource = EmployeesList;
        }

        private void SearchFilterTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            _searchFilter = SearchFilterTextBox.Text.Trim();
            ApplyFilter();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            // 2. Убрали _SqliteDataService из вызова
            var addWin = new AddEditEmployeeWindow(null);
            if (addWin.ShowDialog() == true) _ = LoadEmployeesAsync();
        }

        private void OpenEditEmployee(WpfUser employee)
        {
            // Создаем контрактный объект для API
            var contractUser = new ContractsUser
            {
                Id = employee.Id,
                FullName = employee.FullName,
                Login = employee.Login,
                PasswordHash = employee.PasswordHash,
                Role = employee.Role,
                IsActive = employee.IsActive,
                Phone = employee.Phone,
                BranchId = employee.BranchId,
                BaseWagePercentage = employee.BaseWagePercentage,
                BaseSalaryPerShift = employee.BaseSalaryPerShift
            };

            var editWin = new AddEditEmployeeWindow(contractUser);
            if (editWin.ShowDialog() == true) _ = LoadEmployeesAsync();
        }


        private void EmployeesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => _selectedEmployee = EmployeesListView.SelectedItem as WpfUser;
        private void EmployeesListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        { if (_selectedEmployee != null) OpenEditEmployee(_selectedEmployee); }
        private void EditEmployeeMenuItem_Click(object sender, RoutedEventArgs e)
        { if (_selectedEmployee != null) OpenEditEmployee(_selectedEmployee); }
        private void RefreshButton_Click(object sender, RoutedEventArgs e) => _ = LoadEmployeesAsync();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void ScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            // Явно приводим App.CurrentUser к типу WpfUser, чтобы компилятор не путался
            WpfUser currentUser = App.CurrentUser as WpfUser;

            if (currentUser == null)
            {
                MessageBox.Show("Ошибка: Пользователь не авторизован", "Ошибка");
                return;
            }

            var scheduleWin = new ScheduleWindow(currentUser);
            scheduleWin.ShowDialog();
        }


    }
}
