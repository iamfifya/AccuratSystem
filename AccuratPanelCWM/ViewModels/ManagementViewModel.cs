using AccuratPanelCWM.Services;
using AccuratPanelCWM.Views;
using AccuratSystem.Contracts.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Microsoft.Maui.Storage;

namespace AccuratPanelCWM.ViewModels
{
    public partial class EmployeeSelectionItem : ObservableObject
    {
        public int Id { get; set; }
        [ObservableProperty] private string _fullName;
        [ObservableProperty] private bool _isSelected;
        public string RoleDisplay { get; set; }
    }

    public partial class ManagementViewModel : ObservableObject
    {
        private readonly ApiService _apiService;
        private Shift _currentShift;
        private readonly IServiceProvider _serviceProvider;
        private List<EmployeeSelectionItem> _allEmployeesCache = new();

        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private bool _isShiftOpen;
        public bool IsShiftClosed => !IsShiftOpen;

        [ObservableProperty] private string _statusIcon;
        [ObservableProperty] private string _statusTitle;
        [ObservableProperty] private string _statusDescription;
        [ObservableProperty] private string _actionButtonText;
        [ObservableProperty] private Color _actionButtonColor;
        [ObservableProperty] private string _revenueDisplay = "0 ₽";
        [ObservableProperty] private string _cashInHandDisplay = "0 ₽";

        // ИСПОЛЬЗУЕМ ОБЫЧНЫЕ СВОЙСТВА ВМЕСТО ГЕНЕРАТОРА ДЛЯ СЛОЖНОЙ ЛОГИКИ
        private DateTime _selectedShiftDate = DateTime.Today;
        public DateTime SelectedShiftDate
        {
            get => _selectedShiftDate;
            set
            {
                _selectedShiftDate = value;
                OnPropertyChanged(nameof(SelectedShiftDate));
            }
        }

        private string _employeeSearchText;
        public string EmployeeSearchText
        {
            get => _employeeSearchText;
            set
            {
                _employeeSearchText = value;
                OnPropertyChanged(nameof(EmployeeSearchText));
                FilterEmployees(value); // Вызываем фильтрацию напрямую
            }
        }

        public ObservableCollection<EmployeeSelectionItem> SelectableEmployees { get; } = new();

        public ManagementViewModel(ApiService apiService, IServiceProvider serviceProvider)
        {
            _apiService = apiService;
            _serviceProvider = serviceProvider;
            LoadShiftDataCommand.Execute(null);
        }

        // Исправленный метод фильтрации (решает ошибку CS0019)
        private void FilterEmployees(string filter)
        {
            string search = filter?.ToLower().Trim() ?? "";

            var filtered = string.IsNullOrWhiteSpace(search)
                ? _allEmployeesCache
                : _allEmployeesCache.Where(e =>
                    (e.FullName?.ToLower().Contains(search) ?? false) ||
                    (e.RoleDisplay?.ToLower().Contains(search) ?? false)).ToList();

            SelectableEmployees.Clear();
            foreach (var emp in filtered) SelectableEmployees.Add(emp);
        }

        // 1. Создаем приватный метод для самой загрузки (без проверки IsBusy)
        private async Task InternalLoadDataAsync()
        {
            try
            {
                int currentBranchId = Preferences.Default.Get("CurrentBranchId", 0);
                var shifts = await _apiService.GetShiftsAsync();
                _currentShift = shifts.FirstOrDefault(s => !s.IsClosed && s.BranchId == currentBranchId);

                if (_currentShift != null)
                {
                    IsShiftOpen = true;
                    StatusIcon = "🟢";
                    StatusTitle = $"Смена от {_currentShift.Date:dd.MM.yyyy}";
                    StatusDescription = $"Начата в {_currentShift.StartTime:HH:mm}";
                    ActionButtonText = "Закрыть смену";
                    ActionButtonColor = Color.FromArgb("#E74C3C");

                    var cashbox = await _apiService.GetShiftCashboxSummaryAsync(_currentShift.Id);
                    CashInHandDisplay = $"{cashbox.CashInHand:N0} ₽";

                    var orders = await _apiService.GetOrdersAsync();
                    var shiftOrders = orders.Where(o => o.ShiftId == _currentShift.Id && (o.Status == "Завершен" || o.Status == "Выполнен")).ToList();
                    RevenueDisplay = $"{shiftOrders.Sum(o => o.FinalPrice):N0} ₽";
                }
                else
                {
                    IsShiftOpen = false;
                    StatusIcon = "🔒";
                    StatusTitle = "Смена закрыта";
                    StatusDescription = "Откройте смену, чтобы начать принимать заказы";
                    ActionButtonText = "Начать смену";
                    ActionButtonColor = Color.FromArgb("#27AE60");

                    var allUsers = await _apiService.GetUsersAsync();
                    _allEmployeesCache = allUsers.Where(u => u.IsActive).Select(u => new EmployeeSelectionItem
                    {
                        Id = u.Id,
                        FullName = u.FullName,
                        RoleDisplay = u.Role?.Name ?? "Сотрудник",
                        IsSelected = false
                    }).ToList();

                    SelectableEmployees.Clear();
                    foreach (var emp in _allEmployeesCache) SelectableEmployees.Add(emp);
                }
                OnPropertyChanged(nameof(IsShiftClosed));
            }
            catch (Exception ex)
            {
                // Логгируем ошибку, но не блокируем интерфейс
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления смены: {ex.Message}");
            }
        }

        // 2. Публичная команда для RefreshView (с проверкой IsBusy)
        [RelayCommand]
        private async Task LoadShiftDataAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                await InternalLoadDataAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ActionShiftAsync()
        {
            if (_currentShift == null)
            {
                // 1. Проверяем, выбраны ли сотрудники
                var selectedIds = _allEmployeesCache.Where(x => x.IsSelected).Select(x => x.Id).ToList();
                if (!selectedIds.Any())
                {
                    await Application.Current.MainPage.DisplayAlert("Внимание", "Выберите хотя бы одного сотрудника!", "OK");
                    return;
                }

                // 2. Получаем ID текущего филиала
                int branchId = Preferences.Default.Get("CurrentBranchId", 0);
                if (branchId == 0)
                {
                    await Application.Current.MainPage.DisplayAlert("Ошибка", "Филиал не выбран. Пожалуйста, выберите филиал в настройках или на главной странице.", "ОК");
                    return;
                }

                // 3. Проверка на уже открытую смену (Логика из WPF)
                var shifts = await _apiService.GetShiftsAsync();
                var openShift = shifts.FirstOrDefault(s => !s.IsClosed && s.BranchId == branchId);
                if (openShift != null)
                {
                    bool confirm = await Application.Current.MainPage.DisplayAlert(
                        "Конфликт смен",
                        $"На выбранном филиале уже есть открытая смена от {openShift.Date:dd.MM.yyyy}. Закрыть её перед открытием новой?",
                        "Да, закрыть", "Отмена");

                    if (confirm)
                    {
                        await _apiService.CloseShiftAsync(openShift.Id);
                    }
                    else return;
                }

                // 4. Финальное подтверждение
                bool finalConfirm = await Application.Current.MainPage.DisplayAlert(
                    "Открытие",
                    $"Начать новую смену от {SelectedShiftDate:dd.MM.yyyy}?",
                    "Да", "Отмена");

                if (!finalConfirm) return;

                IsBusy = true;
                try
                {
                    // !!! ВАЖНО: ЗАПОЛНЯЕМ ВСЕ ПОЛЯ ОБЪЕКТА !!!
                    var newShift = new Shift
                    {
                        BranchId = branchId,                             // Исправляет ошибку FK_Shifts_Branches_BranchId
                        Date = DateTime.SpecifyKind(SelectedShiftDate.Date, DateTimeKind.Utc),
                        StartTime = DateTime.UtcNow,
                        IsClosed = false,
                        EmployeeIds = selectedIds,
                        Notes = ""
                    };

                    await _apiService.OpenShiftAsync(newShift);

                    // Обновляем данные на экране без повторной проверки IsBusy
                    await InternalLoadDataAsync();

                    await Application.Current.MainPage.DisplayAlert("Успех", "Смена успешно открыта!", "ОК");
                }
                catch (Exception ex)
                {
                    await Application.Current.MainPage.DisplayAlert("Ошибка", $"Не удалось открыть смену: {ex.Message}", "ОК");
                }
                finally
                {
                    IsBusy = false;
                }
            }
            else
            {
                // --- ЛОГИКА ЗАКРЫТИЯ СМЕНЫ ---
                bool confirm = await Application.Current.MainPage.DisplayAlert(
                    "Осторожно",
                    "Вы уверены, что хотите закрыть смену? Это действие нельзя отменить.",
                    "Закрыть", "Отмена");

                if (confirm)
                {
                    IsBusy = true;
                    try
                    {
                        // Проверка на активные заказы (как в WPF)
                        var orders = await _apiService.GetOrdersAsync();
                        if (orders.Any(o => o.ShiftId == _currentShift.Id && o.Status == "В работе"))
                        {
                            await Application.Current.MainPage.DisplayAlert("Внимание", "Нельзя закрыть смену! Есть активные заказы в работе.", "ОК");
                            return;
                        }

                        await _apiService.CloseShiftAsync(_currentShift.Id);
                        await InternalLoadDataAsync();
                        await Application.Current.MainPage.DisplayAlert("Успех", "Смена закрыта!", "ОK");
                    }
                    catch (Exception ex)
                    {
                        await Application.Current.MainPage.DisplayAlert("Ошибка", ex.Message, "ОK");
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }
            }
        }


        [RelayCommand]
        private async Task OpenCashboxAsync()
        {
            if (_currentShift == null) return;
            var cashboxPage = _serviceProvider.GetRequiredService<CashboxPage>();
            var vm = (CashboxViewModel)cashboxPage.BindingContext;
            vm.Initialize(_currentShift.Id);
            await Application.Current.MainPage.Navigation.PushModalAsync(cashboxPage);
        }
    }
}
