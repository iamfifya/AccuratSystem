using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace AccuratPanelCarWashing
{
    public partial class StartShiftWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly ApiService _apiService;
        private DateTime _selectedDate;
        private List<EmployeeSelection> _employees;

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
                var allEmployees = await _apiService.GetUsersAsync();
                Employees = allEmployees.Where(e => e.IsActive).Select(e => new EmployeeSelection
                {
                    Id = e.Id,
                    FullName = e.FullName,
                    IsAdmin = e.IsAdmin,
                    IsSelected = false
                }).ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки сотрудников: {ex.Message}");
            }
        }

        private async void StartShiftButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedIds = Employees.Where(emp => emp.IsSelected).Select(emp => emp.Id).ToList();

                if (!selectedIds.Any())
                {
                    MessageBox.Show("Выберите сотрудников", "Внимание");
                    return;
                }

                this.IsEnabled = false;

                // 1. Проверяем открытые смены через API
                var openShift = await _apiService.GetCurrentOpenShiftAsync();
                if (openShift != null)
                {
                    var result = MessageBox.Show($"Уже есть открытая смена. Закрыть её?",
                        "Внимание", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                        await _apiService.CloseShiftAsync(openShift.Id);
                    else return;
                }

                // 2. Создаем новую смену для ВЫБРАННОГО филиала
                var newShift = new Shift
                {
                    // Берем ID филиала из настроек сессии, которые поставили в LoginWindow
                    BranchId = AppSettings.CurrentBranchId,

                    // Дата в формате UTC для Postgres
                    Date = DateTime.SpecifyKind(SelectedDate.Date, DateTimeKind.Utc),

                    EmployeeIds = selectedIds,
                    IsClosed = false,
                    Notes = "" // Не шлем null, шлем пустую строку
                };

                await _apiService.OpenShiftAsync(newShift);

                MessageBox.Show($"Смена открыта на филиале: {AppSettings.CurrentBranchName}");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                // Выводим детали, если сервер прислал описание ошибки
                MessageBox.Show($"Ошибка сервера (400): {ex.Message}");
            }
            finally { this.IsEnabled = true; }
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
        public bool IsAdmin { get; set; }
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }
    }
}
