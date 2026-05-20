using AccuratSystem.Contracts.Models;
using AccuratPanelCarWashing.Services;
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
        private ApiService _apiService; // Только API!
        private List<User> _allEmployees;
        private List<User> _employeesList;
        private string _searchFilter = "";
        private User _selectedEmployee;

        public List<User> EmployeesList
        {
            get => _employeesList;
            set { _employeesList = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EmployeesList))); }
        }

        // 1. Убрали SqliteDataService из конструктора
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
                _allEmployees = await _apiService.GetUsersAsync();
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

            // 1. Сначала фильтруем по поиску (если есть)
            if (!string.IsNullOrWhiteSpace(_searchFilter))
            {
                filtered = filtered.Where(e =>
                    e.FullName.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.Login.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            // 2. КАСТОМНАЯ СОРТИРОВКА
            // Порядок: Директоры (1), Админы (2), Мойщики (4), Автосервис (3)
            filtered = filtered.OrderBy(e =>
            {
                switch (e.Role)
                {
                    case 1: return 1; // Директор - первый
                    case 2: return 2; // Админ - второй
                    case 4: return 3; // Мойщик - третий
                    case 3: return 4; // Сервис - четвертый
                    default: return 5; // Все остальные (если появятся) - в конец
                }
            })
            .ThenBy(e => e.FullName); // Если должности одинаковые, сортируем по алфавиту

            // 3. Обновляем UI
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

        private void OpenEditEmployee(User employee)
        {
            // 3. Убрали _SqliteDataService из вызова
            var editWin = new AddEditEmployeeWindow(employee);
            if (editWin.ShowDialog() == true) _ = LoadEmployeesAsync();
        }

        private void EmployeesListView_SelectionChanged(object sender, SelectionChangedEventArgs e) => _selectedEmployee = EmployeesListView.SelectedItem as User;
        private void EmployeesListView_MouseDoubleClick(object sender, MouseButtonEventArgs e) { if (_selectedEmployee != null) OpenEditEmployee(_selectedEmployee); }
        private void EditEmployeeMenuItem_Click(object sender, RoutedEventArgs e) { if (_selectedEmployee != null) OpenEditEmployee(_selectedEmployee); }
        private void RefreshButton_Click(object sender, RoutedEventArgs e) => _ = LoadEmployeesAsync();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void ScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            // 4. ИСПРАВЛЕННАЯ СТРОКА: Вызываем график без аргументов!
            var scheduleWin = new ScheduleWindow();
            scheduleWin.ShowDialog();
        }
    }
}
