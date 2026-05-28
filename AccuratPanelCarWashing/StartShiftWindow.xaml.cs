using AccuratSystem.Contracts.Models;
using AccuratPanelCarWashing.Services;
using AccuratPanelCarWashing.Models;
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
                var allEmployees = await _apiService.GetUsersAsync();

                //  Формируем нормальное название должности сразу
                Employees = allEmployees.Where(e => e.IsActive).Select(e => new EmployeeSelection
                {
                    Id = e.Id,
                    FullName = e.FullName,
                    RoleDisplay = GetRoleName(e.Role), // Формируем строку
                    IsSelected = false
                }).ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки сотрудников: {ex.Message}");
            }
        }

        // Вспомогательный метод для правильного маппинга ролей
        private string GetRoleName(int role)
        {
            switch (role)
            {
                case 1: return "Директор";
                case 2: return "Администратор";
                case 3: return "Мойщик";
                case 4: return "Сотрудник сервиса";
                default: return "Сотрудник";
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

        //  Добавили свойство для отображения должности
        public string RoleDisplay { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }
    }
}