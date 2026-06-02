using AccuratPanelCWM.Services;
using AccuratPanelCWM.Views;
using AccuratSystem.Contracts.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AccuratPanelCWM.ViewModels
{
    // Вспомогательный класс для чекбоксов выбора сотрудников
    public partial class EmployeeSelectionItem : ObservableObject
    {
        public int Id { get; set; }
        [ObservableProperty] private string _fullName;
        [ObservableProperty] private bool _isSelected;
    }

    public partial class ManagementViewModel : ObservableObject
    {
        private readonly ApiService _apiService;
        private Shift _currentShift;
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty] private bool _isBusy;

        // --- Свойства для привязки интерфейса ---
        [ObservableProperty] private bool _isShiftOpen;
        public bool IsShiftClosed => !IsShiftOpen; // Инверсия для скрытия/показа блоков

        [ObservableProperty] private string _statusIcon;
        [ObservableProperty] private string _statusTitle;
        [ObservableProperty] private string _statusDescription;

        [ObservableProperty] private string _actionButtonText;
        [ObservableProperty] private Color _actionButtonColor;

        [ObservableProperty] private string _revenueDisplay = "0 ₽";
        [ObservableProperty] private string _cashInHandDisplay = "0 ₽";

        public ObservableCollection<EmployeeSelectionItem> SelectableEmployees { get; } = new();

        public ManagementViewModel(ApiService apiService, IServiceProvider serviceProvider)
        {
            _apiService = apiService;
            _serviceProvider = serviceProvider;
            LoadShiftDataCommand.Execute(null);
        }

        [RelayCommand]
        private async Task LoadShiftDataAsync()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                int currentBranchId = Preferences.Default.Get("CurrentBranchId", 0);

                var shifts = await _apiService.GetShiftsAsync();
                _currentShift = shifts.FirstOrDefault(s => !s.IsClosed && s.BranchId == currentBranchId);

                if (_currentShift != null)
                {
                    // === СМЕНА ОТКРЫТА ===
                    IsShiftOpen = true;
                    StatusIcon = "🟢";
                    StatusTitle = $"Смена от {_currentShift.Date:dd.MM.yyyy}";
                    StatusDescription = $"Начата в {_currentShift.StartTime:HH:mm}";

                    ActionButtonText = "Закрыть смену";
                    ActionButtonColor = Color.FromArgb("#E74C3C"); // Красный

                    // Грузим финансы
                    var cashbox = await _apiService.GetShiftCashboxSummaryAsync(_currentShift.Id);
                    CashInHandDisplay = $"{cashbox.CashInHand:N0} ₽";

                    var orders = await _apiService.GetOrdersAsync();
                    var shiftOrders = orders.Where(o => o.ShiftId == _currentShift.Id && (o.Status == "Завершен" || o.Status == "Выполнен")).ToList();
                    RevenueDisplay = $"{shiftOrders.Sum(o => o.FinalPrice):N0} ₽";
                }
                else
                {
                    // === СМЕНА ЗАКРЫТА ===
                    IsShiftOpen = false;
                    StatusIcon = "🔒";
                    StatusTitle = "Смена закрыта";
                    StatusDescription = "Откройте смену, чтобы начать принимать заказы";

                    ActionButtonText = "Начать смену";
                    ActionButtonColor = Color.FromArgb("#27AE60"); // Зеленый

                    // Грузим сотрудников для выбора
                    var allUsers = await _apiService.GetUsersAsync();
                    SelectableEmployees.Clear();
                    foreach (var u in allUsers.Where(u => u.IsActive))
                    {
                        SelectableEmployees.Add(new EmployeeSelectionItem { Id = u.Id, FullName = u.FullName, IsSelected = false });
                    }
                }

                // Уведомляем UI, что IsShiftClosed тоже изменилось
                OnPropertyChanged(nameof(IsShiftClosed));
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", $"Сбой сети: {ex.Message}", "ОК");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task ActionShiftAsync()
        {
            if (_currentShift == null)
            {
                // ОТКРЫТИЕ СМЕНЫ
                var selectedIds = SelectableEmployees.Where(x => x.IsSelected).Select(x => x.Id).ToList();
                if (!selectedIds.Any())
                {
                    await Application.Current.MainPage.DisplayAlert("Внимание", "Выберите хотя бы одного сотрудника!", "OK");
                    return;
                }

                bool confirm = await Application.Current.MainPage.DisplayAlert("Открытие", "Начать новую смену?", "Да", "Отмена");
                if (confirm)
                {
                    IsBusy = true;
                    try
                    {
                        var newShift = new Shift
                        {
                            BranchId = Preferences.Default.Get("CurrentBranchId", 0),
                            Date = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc),
                            StartTime = DateTime.UtcNow,
                            IsClosed = false,
                            EmployeeIds = selectedIds,
                            Notes = ""
                        };

                        await _apiService.OpenShiftAsync(newShift);
                        await LoadShiftDataAsync(); // Перезагружаем интерфейс
                    }
                    catch (Exception ex) { await Application.Current.MainPage.DisplayAlert("Ошибка", ex.Message, "OK"); }
                    finally { IsBusy = false; }
                }
            }
            else
            {
                // ЗАКРЫТИЕ СМЕНЫ
                bool confirm = await Application.Current.MainPage.DisplayAlert("Осторожно", "Вы уверены, что хотите закрыть смену? Это действие нельзя отменить.", "Закрыть", "Отмена");
                if (confirm)
                {
                    IsBusy = true;
                    try
                    {
                        var orders = await _apiService.GetOrdersAsync();
                        if (orders.Any(o => o.ShiftId == _currentShift.Id && o.Status == "В работе"))
                        {
                            await Application.Current.MainPage.DisplayAlert("Внимание", "Нельзя закрыть смену! Завершите или отмените все активные заказы.", "ОК");
                            return;
                        }

                        await _apiService.CloseShiftAsync(_currentShift.Id);
                        await LoadShiftDataAsync(); // Перезагружаем интерфейс
                        await Application.Current.MainPage.DisplayAlert("Успех", "Смена успешно закрыта!", "ОК");
                    }
                    catch (Exception ex) { await Application.Current.MainPage.DisplayAlert("Ошибка", ex.Message, "ОК"); }
                    finally { IsBusy = false; }
                }
            }
        }

        [RelayCommand]
        private async Task OpenCashboxAsync()
        {
            if (_currentShift == null) return;

            // Просим DI-контейнер собрать страницу
            var cashboxPage = _serviceProvider.GetRequiredService<CashboxPage>();

            // Передаем ID смены
            var vm = (CashboxViewModel)cashboxPage.BindingContext;
            vm.Initialize(_currentShift.Id);

            // Открываем модально
            await Application.Current.MainPage.Navigation.PushModalAsync(cashboxPage);
        }
    }
}