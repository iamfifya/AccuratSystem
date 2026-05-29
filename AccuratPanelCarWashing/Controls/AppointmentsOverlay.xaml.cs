// === ЯВНЫЕ АЛИАСЫ ДЛЯ РАЗРЕШЕНИЯ КОНФЛИКТОВ ИМЁН ===
// Контрактные модели из API
using ContractsOrder = AccuratSystem.Contracts.Models.Order;
using ContractsService = AccuratSystem.Contracts.Models.Service;
using ContractsUser = AccuratSystem.Contracts.Models.User;
using ContractsBranch = AccuratSystem.Contracts.Models.Branch;
using ContractsShift = AccuratSystem.Contracts.Models.Shift;

// UI-модели
using WpfUser = AccuratPanelCarWashing.Models.User;
using WpfAppointment = AccuratPanelCarWashing.Models.Appointment;

// Остальные using
using AccuratPanelCarWashing.Controls;
using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services; // <-- ВАЖНО: для методов расширения (GetWasherId)
using AccuratPanelCarWashing.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AccuratPanelCarWashing.Controls
{
    public partial class AppointmentsOverlay : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly ApiService _apiService = new ApiService();
        private ContractsShift _currentShift; // Используем контрактный тип для смен
        private OrderDisplayItem _selectedItem;
        public event Action<OrderDisplayItem> OnEditRequested;

        public ContractsShift CurrentShift
        {
            get => _currentShift;
            set
            {
                _currentShift = value;
                System.Diagnostics.Debug.WriteLine($"AppointmentsOverlay: CurrentShift updated (Id={_currentShift?.Id})");
            }
        }

        public ObservableCollection<BranchTabItem> BranchTabs { get; set; } = new ObservableCollection<BranchTabItem>();
        private BranchTabItem _selectedBranchTab;
        public BranchTabItem SelectedBranchTab
        {
            get => _selectedBranchTab;
            set { _selectedBranchTab = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedBranchTab))); }
        }

        public OrderDisplayItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem != null) _selectedItem.IsSelected = false;
                _selectedItem = value;
                if (_selectedItem != null) _selectedItem.IsSelected = true;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItem)));
            }
        }

        public AppointmentsOverlay()
        {
            InitializeComponent();
            DataContext = this;

            FilterDatePicker.SelectedDateChanged += FilterDatePicker_SelectedDateChanged;
            FilterDatePicker.SelectedDate = DateTime.Now;

            // 🔍 Отладка: логгируем все клики
            this.PreviewMouseLeftButtonDown += (s, e) =>
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Клик в оверлее: {e.GetPosition(this)}");
        }

        private void FilterDatePicker_SelectedDateChanged(object sender, DateTime? selectedDate)
        {
            _ = LoadAppointmentsAsync();
        }

        public void Show()
        {
            this.Visibility = Visibility.Visible;
            OverlayBackground.Visibility = Visibility.Visible;
            PopupPanel.Visibility = Visibility.Visible;

            if (BranchTabs.Count == 0)
                _ = LoadBranchesAndAppointmentsAsync();
            else
                _ = LoadAppointmentsAsync();

            var showAnimation = Resources["ShowAnimation"] as Storyboard;
            showAnimation?.Begin();
        }

        private async Task LoadBranchesAndAppointmentsAsync()
        {
            try
            {
                var allBranches = await _apiService.GetBranchesAsync();
                var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();

                // ИСПРАВЛЕНО: Проверяем Role напрямую, так как _currentUser может быть контрактным типом
                bool isAdminOrDirector = false;
                if (mainWindow != null)
                {
                    // Если в MainWindow _currentUser типа WpfUser — используем IsAdmin
                    // Если контрактный — проверяем Role
                    var currentUserField = typeof(MainWindow).GetField("_currentUser",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (currentUserField != null)
                    {
                        var currentUser = currentUserField.GetValue(mainWindow);
                        if (currentUser is WpfUser wpfUser)
                            isAdminOrDirector = wpfUser.IsAdmin;
                        else if (currentUser is ContractsUser contractsUser)
                            isAdminOrDirector = contractsUser.RoleId == 1 || contractsUser.RoleId == 2;
                    }
                }

                BranchTabs.Clear();
                if (isAdminOrDirector)
                {
                    foreach (var b in allBranches)
                    {
                        var tab = new BranchTabItem { BranchId = b.Id, BranchName = b.Name };
                        PopulateZones(tab, b);
                        BranchTabs.Add(tab);
                    }
                }
                else
                {
                    var myBranch = allBranches.FirstOrDefault(b => b.Id == AppSettings.CurrentBranchId);
                    if (myBranch != null)
                    {
                        var tab = new BranchTabItem { BranchId = myBranch.Id, BranchName = myBranch.Name };
                        PopulateZones(tab, myBranch);
                        BranchTabs.Add(tab);
                    }
                }

                if (BranchTabs.Any()) SelectedBranchTab = BranchTabs.First();

                await LoadAppointmentsAsync();
            }
            catch (Exception ex) { MessageBox.Show("Ошибка загрузки филиалов: " + ex.Message); }
        }

        private void PopulateZones(BranchTabItem tab, ContractsBranch branch) // Принимаем контрактный филиал
        {
            for (int i = 1; i <= branch.WashBaysCount; i++)
                tab.WashZones.Add(new WorkZone { ZoneNumber = i, ZoneName = $"🚘 БОКС {i}", Department = "Wash" });
            for (int i = 1; i <= branch.ServiceLiftsCount; i++)
                tab.ServiceZones.Add(new WorkZone { ZoneNumber = i, ZoneName = $"🔧 ПОДЪЕМНИК {i}", Department = "Service" });
        }

        public void Hide()
        {
            var hideAnimation = Resources["HideAnimation"] as Storyboard;
            if (hideAnimation != null)
            {
                hideAnimation.Completed += (s, e) =>
                {
                    this.Visibility = Visibility.Collapsed;
                    OverlayBackground.Visibility = Visibility.Collapsed;
                    PopupPanel.Visibility = Visibility.Collapsed;
                };
                hideAnimation.Begin();
            }
            else
            {
                this.Visibility = Visibility.Collapsed;
                OverlayBackground.Visibility = Visibility.Collapsed;
                PopupPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async Task LoadAppointmentsAsync()
        {
            DateTime? filterDate = FilterDatePicker.SelectedDate ?? DateTime.Now;

            try
            {
                var allOrders = await _apiService.GetOrdersAsync();
                var allServices = await _apiService.GetServicesAsync();

                var appointments = allOrders
                    .Where(o => o.IsAppointment && o.Time.Date == filterDate.Value.Date)
                    .OrderBy(o => o.Time)
                    .ToList();

                var displayItems = appointments.Select(a => new OrderDisplayItem
                {
                    Id = a.Id,
                    BranchId = a.BranchId,
                    Department = a.Department,
                    CarModel = a.CarModel,
                    CarNumber = a.CarNumber,
                    Time = TimeHelper.ToMsk(a.Time),
                    ServicesList = string.Join(", ", (a.ServiceIds ?? new List<int>()).Select(id => allServices.FirstOrDefault(s => s.Id == id)?.Name ?? "Unknown")),
                    FinalPrice = a.FinalPrice,
                    ExtraCost = a.ExtraCost,
                    ExtraCostReason = a.ExtraCostReason,
                    BoxNumber = a.BoxNumber,
                    Status = GetAppointmentStatusDisplay(a),
                    IsCompleted = a.Status == "Выполнен" || a.Status == "Отменен",
                    IsAppointment = true,
                    PaymentMethod = a.PaymentMethod,
                    DurationMinutes = a.DurationMinutes > 0 ? a.DurationMinutes : 60,
                }).ToList();

                // Распределяем записи по динамическим вкладкам и боксам
                foreach (var tab in BranchTabs)
                {
                    // Объединяем зоны только для цикла распределения
                    foreach (var zone in tab.WashZones.Concat(tab.ServiceZones))
                    {
                        var ordersForZone = displayItems.Where(i =>
                            i.BranchId == tab.BranchId &&
                            i.BoxNumber == zone.ZoneNumber &&
                            i.Department == zone.Department).ToList();

                        zone.Orders.Clear();
                        foreach (var o in ordersForZone) zone.Orders.Add(o);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки записей: " + ex.Message);
            }
        }

        // === ФОРМАТИРОВАНИЕ СТАТУСА ЗАПИСИ ===
        private string GetAppointmentStatusDisplay(ContractsOrder order) // Принимаем контрактный тип
        {
            // Прошлое время + активный статус = просрочена
            if (order.Time < DateTime.Now &&
                (order.Status == "Предварительная запись" || order.Status == "Ожидает"))
            {
                return "⚠️ Просрочена";
            }

            // ✅ Обычный switch вместо switch expression (для C# 7.3)
            switch (order.Status)
            {
                case "Предварительная запись":
                    return "📅 Ожидает";
                case "В работе":
                    return "🔄 В работе";
                case "Выполнен":
                    return "✅ Выполнен";
                case "Отменен":
                    return "❌ Отменена";
                case "Завершен":
                    return "✅ Завершена";
                default:
                    // Если запись была конвертирована в заказ
                    if (!order.IsAppointment)
                        return "🔄 → Заказ";
                    return $"📋 {order.Status}";
            }
        }

        // === КОНВЕРТАЦИЯ ЗАПИСИ В ЗАКАЗ + ВЫБОР МОЙЩИКА ===
        private async void ConvertToOrderButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem == null)
            {
                MessageBox.Show("Выберите запись для преобразования", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_currentShift == null || _currentShift.IsClosed)
            {
                MessageBox.Show("Нельзя преобразовать запись: нет активной смены!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 1. Грузим мойщиков
                var allUsers = await _apiService.GetUsersAsync();
                var washers = allUsers.Where(u => u.RoleId != 1 && u.RoleId  != 2).ToList();

                if (!washers.Any())
                {
                    MessageBox.Show("Нет доступных мойщиков. Сначала добавьте сотрудников!", "Ошибка");
                    return;
                }

                // 2. Выбираем мойщика
                var washerDialog = new WasherSelectionDialog(washers);
                if (washerDialog.ShowDialog() != true || washerDialog.SelectedWasher == null)
                    return;

                var selectedWasher = washerDialog.SelectedWasher;

                // 3. 💥 ВАЖНО: Вызываем специальный метод API для конвертации!
                var convertedOrder = await _apiService.ConvertAppointmentToOrderAsync(
                    SelectedItem.Id,
                    _currentShift.Id,
                    selectedWasher.Id);

                // 4. Открываем окно редактирования с УЖЕ конвертированным заказом от сервера
                var viewModel = App.GetService<AddEditOrderViewModel>();
                var editWin = new AddEditOrderWindow(viewModel, _currentShift, convertedOrder);

                editWin.Closed += (s, args) =>
                {
                    _ = LoadAppointmentsAsync();
                    var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                    mainWindow?.RefreshData();
                };

                editWin.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при преобразовании: {ex.Message}", "Ошибка API", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // === ОБРАБОТЧИК КЛИКА ДЛЯ НАДЁЖНОГО ВЫБОРА ===
        private void ListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                // Получаем элемент под курсором
                var point = e.GetPosition(listBox);
                var hitTestResult = VisualTreeHelper.HitTest(listBox, point);

                if (hitTestResult?.VisualHit is FrameworkElement element)
                {
                    // Пытаемся получить DataContext (OrderDisplayItem)
                    var dataContext = element.DataContext;

                    // Если элемент — это ListBoxItem, берём его DataContext
                    if (element is ListBoxItem listBoxItem && listBoxItem.DataContext is OrderDisplayItem item)
                    {
                        SelectedItem = item;
                    }
                    // Если элемент внутри ItemTemplate, поднимаемся к родителю ListBoxItem
                    else if (dataContext is OrderDisplayItem displayItem)
                    {
                        SelectedItem = displayItem;
                    }
                }
            }
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e) => _ = LoadAppointmentsAsync();

        private void NewAppointmentButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = App.GetService<AddEditOrderViewModel>();

            // Получаем текущий активный филиал из главного окна
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            int currentBranchId = mainWindow?.SelectedBranchTab?.BranchId ?? AppSettings.CurrentBranchId;

            // Создаем заготовку для новой записи
            var newAppointment = new ContractsOrder
            {
                Id = 0,
                IsAppointment = true,
                Status = "Предварительная запись",
                Time = DateTime.Now.AddDays(1).Date.AddHours(12), // Завтра в 12:00
                PaymentMethod = "Не указано",
                BoxNumber = 1,
                Department = "Wash",
                BodyTypeCategory = 1,
                BranchId = currentBranchId // Явно передаем ID филиала
            };

            var editWin = new AddEditOrderWindow(viewModel, _currentShift, newAppointment);
            editWin.Closed += (s, args) =>
            {
                _ = LoadAppointmentsAsync();
                var mainWin = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                mainWin?.RefreshData();
            };
            editWin.ShowDialog();
        }

        // В обработчике кнопки:
        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] EditButton: SelectedItem.Id={SelectedItem?.Id}, CarNumber={SelectedItem?.CarNumber}");

            if (SelectedItem == null)
            {
                MessageBox.Show("Ничего не выбрано!", "Ошибка");
                return;
            }

            if (OnEditRequested == null)
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] ⚠ OnEditRequested has NO subscribers!");
                MessageBox.Show("Обработчик редактирования не подключен", "Ошибка");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[DEBUG] ✅ Invoking OnEditRequested...");
            OnEditRequested?.Invoke(SelectedItem);
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem == null)
            {
                MessageBox.Show("Выберите запись для удаления", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"Удалить запись для {SelectedItem.CarNumber}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                bool success = await _apiService.DeleteOrderAsync(SelectedItem.Id);
                if (success)
                {
                    MessageBox.Show("Запись удалена", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadAppointmentsAsync(); // Перезагружаем список

                    // Обновляем MainWindow
                    var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                    mainWindow?.RefreshData();
                }
                else
                {
                    MessageBox.Show("Не удалось удалить запись", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AppointmentList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedItem != null) EditButton_Click(sender, null);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();
    }
}