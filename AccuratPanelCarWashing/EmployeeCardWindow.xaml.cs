using AccuratPanelCarWashing.Services;
using AccuratSystem.Contracts.Models; // Используем общие контракты напрямую
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AccuratPanelCarWashing
{
    public partial class EmployeeCardWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly ApiService _apiService;

        // Заменили WpfUser на оригинальный User из контрактов
        private List<User> _allEmployees;
        private List<User> _employeesList;
        private string _searchFilter = "";
        private User _selectedEmployee;

        public List<User> EmployeesList
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
                // Идеальная чистота: никакого ручного маппинга!
                // Все поля (включая Role, CompanyId, PasswordHash) прилетают и сохраняются автоматически.
                _allEmployees = await _apiService.GetUsersAsync();
                ApplyFilter();
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

            // Сортируем по должности, затем по алфавиту
            filtered = filtered.OrderBy(e => e.RoleId).ThenBy(e => e.FullName);

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
            var addWin = new AddEditEmployeeWindow(null);
            if (addWin.ShowDialog() == true) _ = LoadEmployeesAsync();
        }

        private void OpenEditEmployee(User employee)
        {
            // Мы просто передаем объект дальше! 
            // Больше не нужно вручную собирать ContractUser по кусочкам.
            var editWin = new AddEditEmployeeWindow(employee);
            if (editWin.ShowDialog() == true) _ = LoadEmployeesAsync();
        }

        private void EmployeesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => _selectedEmployee = EmployeesListView.SelectedItem as User;

        private void EmployeesListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_selectedEmployee != null) OpenEditEmployee(_selectedEmployee);
        }

        private void EditEmployeeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedEmployee != null) OpenEditEmployee(_selectedEmployee);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => _ = LoadEmployeesAsync();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void ScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            // Здесь App.CurrentUser, если он у тебя всё еще WpfUser, оставляем как есть
            var currentUser = App.CurrentUser as AccuratPanelCarWashing.Models.User;

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