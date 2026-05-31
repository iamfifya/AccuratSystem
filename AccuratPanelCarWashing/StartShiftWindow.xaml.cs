using AccuratSystem.Contracts.Models;
using AccuratPanelCarWashing.Services;
using AccuratPanelCarWashing.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace AccuratPanelCarWashing
{
    public partial class StartShiftWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly ApiService _apiService;
        private DateTime _selectedDate;
        
        // 💥 ПОЛНЫЙ список сотрудников (оригинал, в котором хранятся выбранные галочки)
        private List<EmployeeSelection> _allEmployees = new List<EmployeeSelection>();
        
        // Отфильтрованный список (то, что мы видим на экране прямо сейчас)
        private List<EmployeeSelection> _employees;

        public int? PreselectedBranchId { get; set; }

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set { _selectedDate = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedDate))); }
        }

        public List<EmployeeSelection> Employees
        {
            get => _employees;
            set { _employees = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Employees))); }
        }

        public StartShiftWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            DataContext = this;
            SelectedDate = DateTime.Now.Date;

            _ = LoadEmployeesAsync();
        }

        private async Task LoadEmployeesAsync()
        {
            try
            {
                var allUsers = await _apiService.GetUsersAsync();

                // Заполняем мастер-список
                _allEmployees = allUsers.Where(e => e.IsActive).Select(e => new EmployeeSelection
                {
                    Id = e.Id,
                    FullName = e.FullName,
                    RoleDisplay = e.Role != null ? e.Role.Name : "Сотрудник",
                    IsSelected = false
                }).ToList();

                // Изначально отображаем всех
                Employees = _allEmployees.ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки сотрудников: {ex.Message}");
            }
        }

        // 💥 СОБЫТИЕ ПОИСКА
        private void EmployeeSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = EmployeeSearchTextBox.Text.ToLower().Trim();

            if (string.IsNullOrWhiteSpace(filter))
            {
                // Если поиск пуст — возвращаем всех
                Employees = _allEmployees.ToList();
            }
            else
            {
                // Ищем и по имени, и по должности
                Employees = _allEmployees.Where(emp => 
                    emp.FullName.ToLower().Contains(filter) || 
                    emp.RoleDisplay.ToLower().Contains(filter)
                ).ToList();
            }
        }

        private async void StartShiftButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 💥 ВАЖНО: Берем выбранных из _allEmployees, а не из Employees!
                // Так мы не потеряем тех, кого выбрали, а потом скрыли фильтром поиска.
                var selectedIds = _allEmployees.Where(emp => emp.IsSelected).Select(emp => emp.Id).ToList();

                if (!selectedIds.Any())
                {
                    MessageBox.Show("Выберите сотрудников", "Внимание");
                    return;
                }

                this.IsEnabled = false;

                int targetBranchId = PreselectedBranchId ?? AppSettings.CurrentBranchId;

                var allShifts = await _apiService.GetShiftsAsync();
                var openShift = allShifts.FirstOrDefault(s => !s.IsClosed && s.BranchId == targetBranchId);

                if (openShift != null)
                {
                    var result = MessageBox.Show($"На выбранном филиале уже есть открытая смена. Закрыть её?",
                        "Внимание", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                        await _apiService.CloseShiftAsync(openShift.Id);
                    else return;
                }

                var newShift = new Shift
                {
                    BranchId = targetBranchId,
                    Date = DateTime.SpecifyKind(SelectedDate.Date, DateTimeKind.Utc),
                    EmployeeIds = selectedIds,
                    IsClosed = false,
                    Notes = ""
                };

                await _apiService.OpenShiftAsync(newShift);

                MessageBox.Show($"Смена успешно открыта!", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сервера: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.IsEnabled = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class EmployeeSelection : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public int Id { get; set; }
        public string FullName { get; set; }
        public string RoleDisplay { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }
    }
}