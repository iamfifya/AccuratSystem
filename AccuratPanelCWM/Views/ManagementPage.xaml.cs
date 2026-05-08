using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using AccuratPanelCWM.Models;
using AccuratPanelCWM.Services;

namespace AccuratPanelCWM.Views
{
    public partial class ManagementPage : ContentPage
    {
        private readonly ApiService _apiService;
        private Shift _currentShift;
        private List<EmployeeSelectionModel> _selectableEmployees = new();

        public ManagementPage()
        {
            InitializeComponent();
            _apiService = new ApiService();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadShiftDataAsync();
        }

        private async void RefreshView_Refreshing(object sender, EventArgs e)
        {
            await LoadShiftDataAsync();
        }

        private async Task LoadShiftDataAsync()
        {
            ShiftRefreshView.IsRefreshing = true;
            try
            {
                // 1. Ищем открытую смену строго для ТЕКУЩЕГО филиала
                var shifts = await _apiService.GetShiftsAsync();
                _currentShift = shifts.FirstOrDefault(s => !s.IsClosed && s.BranchId == AppSettings.CurrentBranchId);

                if (_currentShift != null)
                {
                    // --- СМЕНА ОТКРЫТА ---
                    StatusIconLabel.Text = "🟢";
                    StatusTitleLabel.Text = $"Смена от {_currentShift.Date:dd.MM.yyyy}";
                    StatusDescLabel.Text = $"Начата в {_currentShift.StartTime:HH:mm}";

                    ActionShiftButton.Text = "Закрыть смену";
                    ActionShiftButton.BackgroundColor = Color.FromArgb("#E74C3C"); // Красный

                    StatsLayout.IsVisible = true;
                    CashboxButton.IsVisible = true;

                    // Грузим кассу
                    var cashbox = await _apiService.GetShiftCashboxSummaryAsync(_currentShift.Id);
                    CashInHandLabel.Text = $"{cashbox.CashInHand:N0} ₽";

                    // Грузим выручку
                    var orders = await _apiService.GetOrdersAsync();
                    var shiftOrders = orders.Where(o => o.ShiftId == _currentShift.Id && (o.Status == "Завершен" || o.Status == "Выполнен")).ToList();
                    RevenueLabel.Text = $"{shiftOrders.Sum(o => o.FinalPrice):N0} ₽";

                    EmployeeSelectionLayout.IsVisible = false; // Скрываем выбор, если смена уже идет
                }
                else
                {
                    // --- СМЕНА ЗАКРЫТА ---
                    StatusIconLabel.Text = "🔒";
                    StatusTitleLabel.Text = "Смена закрыта";
                    StatusDescLabel.Text = "Откройте смену, чтобы начать принимать заказы";

                    ActionShiftButton.Text = "Начать смену";
                    ActionShiftButton.BackgroundColor = Color.FromArgb("#27AE60"); // Зеленый

                    StatsLayout.IsVisible = false;
                    CashboxButton.IsVisible = false;
                    EmployeeSelectionLayout.IsVisible = true; // Показываем выбор сотрудников

                    // Загружаем сотрудников для выбора
                    var allUsers = await _apiService.GetUsersAsync();

                    //  БЕРЕМ ВООБЩЕ ВСЕХ АКТИВНЫХ (без фильтра по филиалу) 
                    _selectableEmployees = allUsers
                        .Where(u => u.IsActive)
                        .Select(u => new EmployeeSelectionModel { Id = u.Id, FullName = u.FullName, IsSelected = false })
                        .ToList();

                    BindableLayout.SetItemsSource(EmployeesStack, _selectableEmployees);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка", $"Не удалось загрузить данные: {ex.Message}", "ОК");
            }
            finally
            {
                ShiftRefreshView.IsRefreshing = false;
            }
        }

        private async void ActionShiftButton_Clicked(object sender, EventArgs e)
        {
            if (_currentShift == null)
            {
                // Собираем ID выбранных сотрудников
                var selectedIds = _selectableEmployees.Where(x => x.IsSelected).Select(x => x.Id).ToList();

                if (!selectedIds.Any())
                {
                    await DisplayAlert("Внимание", "Выберите хотя бы одного сотрудника!", "OK");
                    return;
                }

                bool confirm = await DisplayAlert("Открытие", "Начать новую смену?", "Да", "Отмена");
                if (confirm)
                {
                    try
                    {
                        ActionShiftButton.IsEnabled = false;
                        var newShift = new Shift
                        {
                            BranchId = AppSettings.CurrentBranchId,
                            Date = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc),
                            StartTime = DateTime.UtcNow,
                            IsClosed = false,
                            EmployeeIds = selectedIds,

                            Notes = "" // Не шлем null, шлем пустую строку
                        };

                        await _apiService.OpenShiftAsync(newShift);
                        await LoadShiftDataAsync();
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlert("Ошибка", ex.Message, "OK");
                    }
                    finally { ActionShiftButton.IsEnabled = true; }
                }
            }
            else
            {
                // === ЗАКРЫТЬ СМЕНУ ===
                bool confirm = await DisplayAlert("Осторожно", "Вы уверены, что хотите закрыть смену? Это действие нельзя отменить.", "Закрыть", "Отмена");
                if (confirm)
                {
                    try
                    {
                        ActionShiftButton.IsEnabled = false;

                        // Проверка 1 в 1 как у тебя в десктопе: есть ли машины "В работе"
                        var orders = await _apiService.GetOrdersAsync();
                        bool hasActive = orders.Any(o => o.ShiftId == _currentShift.Id && o.Status == "В работе");

                        if (hasActive)
                        {
                            await DisplayAlert("Внимание", "Нельзя закрыть смену! Завершите или отмените все активные заказы.", "ОК");
                            return;
                        }

                        await _apiService.CloseShiftAsync(_currentShift.Id);
                        await LoadShiftDataAsync();
                        await DisplayAlert("Успех", "Смена успешно закрыта!", "ОК");
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlert("Ошибка", ex.Message, "ОК");
                    }
                    finally
                    {
                        ActionShiftButton.IsEnabled = true;
                    }
                }
            }
        }

        private async void CashboxButton_Clicked(object sender, EventArgs e)
        {
            if (_currentShift == null) return;
            await Navigation.PushModalAsync(new CashboxPage(_currentShift));
        }
    }

    public class EmployeeSelectionModel : System.ComponentModel.INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string FullName { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

}