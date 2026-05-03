using AccuratPanelCarWashing.Controls;
using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using AccuratPanelCarWashing.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.AspNetCore.SignalR.Client;

namespace AccuratPanelCarWashing
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ApiService _apiService;
        private List<CarWashOrder> _allOrders = new List<CarWashOrder>();
        private List<Appointment> _todayAppointments = new List<Appointment>();
        private Shift _currentShift;
        private User _currentUser;
        private string _searchFilter = "";

        private HubConnection _hubConnection;

        //  Свойства для видимости
        public bool IsDirector => _currentUser?.Role == 1;
        public bool IsSingleBranch => _currentUser?.Role != 1;
        public bool IsAdminOrDirector => _currentUser?.Role == 1 || _currentUser?.Role == 2;

        // Кэши для быстрого UI без постоянных запросов к серверу
        private List<Service> _cachedServices = new List<Service>();
        private List<User> _cachedUsers = new List<User>();
        private List<WasherStat> _washersStats;

        private decimal _companyEarnings;
        private decimal _totalRevenue;

        // Наша главная коллекция вкладок
        private ObservableCollection<BranchTabItem> _branchTabs = new ObservableCollection<BranchTabItem>();
        public ObservableCollection<BranchTabItem> BranchTabs
        {
            get => _branchTabs;
            set { _branchTabs = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BranchTabs))); }
        }

        private BranchTabItem _selectedBranchTab;
        public BranchTabItem SelectedBranchTab
        {
            get => _selectedBranchTab;
            set
            {
                _selectedBranchTab = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedBranchTab)));
                //  Уведомляем интерфейс, что текст филиала тоже изменился
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentBranchDisplay)));
            }
        }

        //  Формируем красивую строку прямо здесь
        public string CurrentBranchDisplay => SelectedBranchTab != null ? $"{SelectedBranchTab.BranchName}" : "";

        public string ActiveUserInfo
        {
            get
            {
                if (_currentUser == null) return "Гость";
                string role = _currentUser.IsAdmin ? "👑 Админ" : "👤 Сотрудник";
                return $"{_currentUser.FullName} • {role}";
            }
        }

        public string CurrentShiftInfo { get; private set; }
        public string TotalOrdersInfo { get; private set; }

        private OrderDisplayItem _selectedItem;
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

        // === СТАТИСТИКА ПО ТИПАМ ОПЛАТЫ ===
        private int _cashCount; private decimal _cashAmount;
        private int _cardCount; private decimal _cardAmount;
        private int _transferCount; private decimal _transferAmount;
        private int _qrCount; private decimal _qrAmount;

        public int QrCount { get => _qrCount; set { _qrCount = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QrCount))); } }
        public decimal QrAmount { get => _qrAmount; set { _qrAmount = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QrAmount))); } }
        public int CashCount { get => _cashCount; set { _cashCount = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CashCount))); } }
        public decimal CashAmount { get => _cashAmount; set { _cashAmount = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CashAmount))); } }
        public int CardCount { get => _cardCount; set { _cardCount = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CardCount))); } }
        public decimal CardAmount { get => _cardAmount; set { _cardAmount = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CardAmount))); } }
        public int TransferCount { get => _transferCount; set { _transferCount = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TransferCount))); } }
        public decimal TransferAmount { get => _transferAmount; set { _transferAmount = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TransferAmount))); } }

        public List<WasherStat> WashersStats { get => _washersStats; set { _washersStats = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WashersStats))); } }
        public decimal CompanyEarnings { get => _companyEarnings; set { _companyEarnings = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CompanyEarnings))); } }
        public decimal TotalRevenue { get => _totalRevenue; set { _totalRevenue = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalRevenue))); } }

        public MainWindow(User user)
        {
            InitializeComponent();
            _apiService = new ApiService();
            _currentUser = user;
            DataContext = this;

            _ = LoadDataAsync();

            InitializeSignalR();
        }

        public void SetUser(User user)
        {
            _currentUser = user;
            _ = LoadDataAsync();
        }

        public void RefreshData() => _ = LoadDataAsync();


        // ==========================================
        // ЗАГРУЗКА ДАННЫХ ИЗ API (ВЫЗЫВАЕТСЯ РЕДКО)
        // ==========================================
        private async Task LoadDataAsync()
        {
            try
            {
                // 1. ЗАГРУЖАЕМ ФИЛИАЛЫ
                var allBranches = await _apiService.GetBranchesAsync();

                BranchTabs.Clear();

                if (IsDirector)
                {
                    // Директор видит все филиалы
                    foreach (var b in allBranches)
                    {
                        BranchTabs.Add(new BranchTabItem
                        {
                            BranchId = b.Id,
                            BranchName = b.Name,
                            BranchWorkZones = GenerateZonesForBranch(b)
                        });
                    }
                }
                else
                {
                    // Остальные видят только свой рабочий филиал
                    var myBranch = allBranches.FirstOrDefault(b => b.Id == AppSettings.CurrentBranchId);
                    if (myBranch != null)
                    {
                        BranchTabs.Add(new BranchTabItem
                        {
                            BranchId = myBranch.Id,
                            BranchName = myBranch.Name,
                            BranchWorkZones = GenerateZonesForBranch(myBranch)
                        });
                    }
                }

                // Выбираем первую вкладку по умолчанию
                if (BranchTabs.Any())
                    SelectedBranchTab = BranchTabs.First();

                // 2. ЗАГРУЖАЕМ ОСТАЛЬНЫЕ ДАННЫЕ
                var allShifts = await _apiService.GetShiftsAsync();
                _cachedUsers = await _apiService.GetUsersAsync();
                _cachedServices = await _apiService.GetServicesAsync();
                var allOrdersFromApi = await _apiService.GetOrdersAsync();

                // Пока берем просто открытую смену
                _currentShift = allShifts.FirstOrDefault(s => !s.IsClosed);

                if (_currentShift != null)
                {
                    _allOrders = allOrdersFromApi.Where(o => o.ShiftId == _currentShift.Id || o.IsAppointment).ToList();
                }
                else
                {
                    _allOrders = allOrdersFromApi.Where(o => o.IsAppointment).ToList();
                }

                ApplyFilterAndDisplay();
                UpdateInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки данных: " + ex.Message);
            }
        }

        // Вспомогательный метод: собирает боксы и подъемники в один список для конкретного филиала
        private ObservableCollection<WorkZone> GenerateZonesForBranch(Branch branch)
        {
            var zones = new ObservableCollection<WorkZone>();

            // Генерируем боксы мойки
            for (int i = 1; i <= branch.WashBaysCount; i++)
                zones.Add(new WorkZone { ZoneNumber = i, ZoneName = $"БОКС {i}", Department = "Wash" });

            // Генерируем подъемники сервиса
            for (int i = 1; i <= branch.ServiceLiftsCount; i++)
                zones.Add(new WorkZone { ZoneNumber = i, ZoneName = $"ПОДЪЕМНИК {i}", Department = "Service" });

            return zones;
        }

        // ==========================================
        // ЛОКАЛЬНАЯ ФИЛЬТРАЦИЯ (МГНОВЕННАЯ)
        // ==========================================

        private void ApplyFilterAndDisplay()
        {
            var filteredOrders = _allOrders.AsEnumerable();
            var filteredApts = _todayAppointments.Where(a => !a.IsCompleted).AsEnumerable();

            if (!string.IsNullOrWhiteSpace(_searchFilter))
            {
                string filter = _searchFilter.ToLower();
                filteredOrders = filteredOrders.Where(o =>
                    (o.CarNumber != null && o.CarNumber.ToLower().Contains(filter)) ||
                    (o.CarModel != null && o.CarModel.ToLower().Contains(filter)));

                filteredApts = filteredApts.Where(a =>
                    (a.CarNumber != null && a.CarNumber.ToLower().Contains(filter)) ||
                    (a.CarModel != null && a.CarModel.ToLower().Contains(filter)));
            }

            var orderItems = filteredOrders.Select(o => new OrderDisplayItem
            {
                Id = o.Id,
                BranchId = o.BranchId,
                Department = o.Department,
                CarModel = o.CarModel,
                CarNumber = o.CarNumber,
                Time = o.Time,
                WasherName = GetWasherName(o.WasherId),
                ServicesList = string.Join(", ", (o.ServiceIds ?? new List<int>()).Select(id => _cachedServices.FirstOrDefault(s => s.Id == id)?.Name ?? "Unknown")),
                FinalPrice = o.FinalPrice,
                OriginalTotalPrice = o.OriginalTotalPrice,
                DiscountPercent = o.DiscountPercent,
                DiscountAmount = o.DiscountAmount,
                ExtraCost = o.ExtraCost,
                ExtraCostReason = o.ExtraCostReason,
                BoxNumber = o.BoxNumber,
                Status = o.Status,
                PaymentMethod = o.PaymentMethod,
                IsAppointment = false,
                IsCompleted = o.Status == "Завершен" || o.Status == "Выполнен",
            });

            var appointmentItems = filteredOrders
                .Where(o => o.IsAppointment && (o.Status == "Предварительная запись" || o.Status == "Запись" || o.Status == "Ожидает"))
                .Select(o => new OrderDisplayItem
                {
                    Id = o.Id,
                    BranchId = o.BranchId,
                    Department = o.Department,
                    CarModel = o.CarModel,
                    CarNumber = o.CarNumber,
                    Time = o.Time,
                    WasherName = o.WasherId > 0 ? GetWasherName(o.WasherId) : "📅 Запись",
                    ServicesList = string.Join(", ", (o.ServiceIds ?? new List<int>()).Select(id => _cachedServices.FirstOrDefault(s => s.Id == id)?.Name ?? "Unknown")),
                    FinalPrice = o.FinalPrice,
                    ExtraCost = o.ExtraCost,
                    ExtraCostReason = o.ExtraCostReason,
                    BoxNumber = o.BoxNumber,
                    Status = o.Status,
                    IsAppointment = true,
                    IsCompleted = o.Status == "Выполнен" || o.Status == "Отменен",
                    PaymentMethod = o.PaymentMethod,
                    DurationMinutes = o.DurationMinutes,
                });

            var allDisplayItems = orderItems.Concat(appointmentItems).OrderBy(i => i.Time).ToList();

            //  САМОЕ ГЛАВНОЕ: Раскидываем заказы по нужным вкладкам и боксам
            foreach (var tab in BranchTabs)
            {
                foreach (var zone in tab.BranchWorkZones)
                {
                    // Ищем заказ, который совпадает по филиалу, номеру бокса и департаменту (Мойка/Сервис)
                    var ordersForZone = allDisplayItems.Where(i =>
                        i.BranchId == tab.BranchId &&
                        i.BoxNumber == zone.ZoneNumber &&
                        i.Department == zone.Department).ToList();

                    zone.Orders.Clear();
                    foreach (var o in ordersForZone)
                    {
                        zone.Orders.Add(o);
                    }
                }
            }
        }

        private string GetWasherName(int? washerId)
        {
            if (washerId == null || washerId == 0) return "Не назначен";
            var washer = _cachedUsers.FirstOrDefault(u => u.Id == washerId);
            return washer?.FullName ?? "Не назначен";
        }

        private void UpdateInfo()
        {
            if (_currentShift != null && !_currentShift.IsClosed)
            {
                CurrentShiftInfo = $"📅 Смена: {_currentShift.Date:dd.MM.yyyy} | Начало: {_currentShift.StartTime:HH:mm}";

                var completedOrders = _allOrders.Where(o => o.Status == "Выполнен").ToList();
                TotalRevenue = completedOrders.Sum(o => o.FinalPrice);

                var totalWasherEarnings = completedOrders.Sum(o => OrderMath.Calculate(o, _cachedServices).WasherEarnings);
                CompanyEarnings = completedOrders.Sum(o => OrderMath.Calculate(o, _cachedServices).CompanyEarnings);

                var inProgressCount = _allOrders.Count(o => o.Status == "Выполняется");
                var cancelledCount = _allOrders.Count(o => o.Status == "Отменен");

                TotalOrdersInfo = $"🚗 Выполнено: {completedOrders.Count} | 🟢 В работе: {inProgressCount} | ❌ Отменено: {cancelledCount} | 👤 Мойщикам: {totalWasherEarnings:N0} ₽";
            }
            else if (_currentShift != null && _currentShift.IsClosed)
            {
                CurrentShiftInfo = $"📅 Смена закрыта: {_currentShift.Date:dd.MM.yyyy}";
                var completedOrders = _allOrders.Where(o => o.Status == "Выполнен").ToList();
                TotalRevenue = completedOrders.Sum(o => o.FinalPrice);
                TotalOrdersInfo = $"🚗 Итого за смену: {completedOrders.Count} машин | 💰 {TotalRevenue:N0} ₽";
                CompanyEarnings = 0;
            }
            else
            {
                CurrentShiftInfo = "⏰ Нет активной смены. Начните смену!";
                TotalOrdersInfo = "";
                TotalRevenue = 0;
                CompanyEarnings = 0;
            }

            UpdateWashersStats();
            UpdatePaymentStats();

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentShiftInfo)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalOrdersInfo)));
        }

        private void UpdatePaymentStats()
        {
            if (_allOrders == null || !_allOrders.Any())
            {
                CashCount = 0; CashAmount = 0; CardCount = 0; CardAmount = 0;
                TransferCount = 0; TransferAmount = 0; QrCount = 0; QrAmount = 0; return;
            }

            CashCount = _allOrders.Count(o => o.PaymentMethod == "Наличные");
            CashAmount = _allOrders.Where(o => o.PaymentMethod == "Наличные").Sum(o => o.FinalPrice);
            CardCount = _allOrders.Count(o => o.PaymentMethod == "Карта");
            CardAmount = _allOrders.Where(o => o.PaymentMethod == "Карта").Sum(o => o.FinalPrice);
            TransferCount = _allOrders.Count(o => o.PaymentMethod == "Перевод");
            TransferAmount = _allOrders.Where(o => o.PaymentMethod == "Перевод").Sum(o => o.FinalPrice);
            QrCount = _allOrders.Count(o => o.PaymentMethod == "QR-код");
            QrAmount = _allOrders.Where(o => o.PaymentMethod == "QR-код").Sum(o => o.FinalPrice);
        }

        private void UpdateWashersStats()
        {
            if (_currentShift == null || _currentShift.IsClosed || !_allOrders.Any())
            {
                WashersStats = new List<WasherStat>();
                return;
            }

            var completedOrders = _allOrders.Where(o => o.Status == "Выполнен").ToList();
            var totalShiftRevenue = completedOrders.Sum(o => OrderMath.Calculate(o, _cachedServices).FinalPrice);

            WashersStats = completedOrders.Where(o => o.WasherId > 0).GroupBy(o => o.WasherId).Select(g =>
            {
                var washerRevenue = g.Sum(o => OrderMath.Calculate(o, _cachedServices).FinalPrice);
                return new WasherStat
                {
                    WasherName = GetWasherName(g.Key),
                    CarsCount = g.Count(),
                    Earnings = g.Sum(o => OrderMath.Calculate(o, _cachedServices).WasherEarnings),
                    TotalRevenue = washerRevenue,
                    Percentage = totalShiftRevenue > 0 ? (washerRevenue / totalShiftRevenue) * 100m : 0m
                };
            }).OrderByDescending(s => s.Earnings).ToList();
        }

        // ==========================================
        // ПОИСК (БЕЗ СПАМА НА СЕРВЕР)
        // ==========================================
        private void SearchFilterTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchFilterTextBox.Text == "🔍 Поиск по гос. номеру или модели...")
            {
                SearchFilterTextBox.Text = "";
                SearchFilterTextBox.Foreground = Brushes.Black;
            }
        }

        private void SearchFilterTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchFilterTextBox.Text))
            {
                SearchFilterTextBox.Text = "🔍 Поиск по гос. номеру или модели...";
                SearchFilterTextBox.Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141));
                _searchFilter = "";
                ApplyFilterAndDisplay();
            }
        }

        private void SearchFilterTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            _searchFilter = SearchFilterTextBox.Text.Trim();
            if (_searchFilter == "🔍 Поиск по гос. номеру или модели...") _searchFilter = "";

            // Вызываем локальную фильтрацию!
            ApplyFilterAndDisplay();
        }

        // ==========================================
        // ОБРАБОТЧИКИ КНОПОК
        // ==========================================
        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentShift == null || _currentShift.IsClosed)
            {
                MessageBox.Show("Сначала начните смену!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var orderViewModel = App.GetService<AddEditOrderViewModel>();
            var addWin = new AddEditOrderWindow(orderViewModel, _currentShift);
            if (addWin.ShowDialog() == true) _ = LoadDataAsync();
        }

        private void OpenEditOrder(OrderDisplayItem orderDisplay)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] OpenEditOrder: Id={orderDisplay.Id}, CarNumber={orderDisplay.CarNumber}");

            // Ищем в кэше
            var originalOrder = _allOrders.FirstOrDefault(o => o.Id == orderDisplay.Id);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Найдено в кэше: {originalOrder != null}");

            // Если не нашли — пробуем загрузить с сервера
            if (originalOrder == null && orderDisplay.Id > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Пытаемся загрузить с сервера...");
                // Можно добавить асинхронную загрузку, но для простоты пока предупреждение
                MessageBox.Show($"Заказ #{orderDisplay.Id} не найден в кэше.\nПопробуйте обновить данные (кнопка 🔄).", "Предупреждение");
                return;
            }

            if (originalOrder != null)
            {
                var orderViewModel = App.GetService<AddEditOrderViewModel>();
                var orderEditWin = new AddEditOrderWindow(orderViewModel, _currentShift, originalOrder);
                if (orderEditWin.ShowDialog() == true) _ = LoadDataAsync();
            }
        }

        private async void DeleteOrderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem == null) return;

            var originalOrder = _allOrders.FirstOrDefault(o => o.Id == SelectedItem.Id);
            if (originalOrder != null && MessageBox.Show("Отменить " + (originalOrder.IsAppointment ? "запись " : "заказ ") + originalOrder.CarNumber + "?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    originalOrder.Status = "Отменен";
                    await _apiService.UpdateOrderAsync(originalOrder);
                    _ = LoadDataAsync();
                }
                catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
            }
        }

        private void EmployeesButton_Click(object sender, RoutedEventArgs e) => App.GetService<EmployeeCardWindow>().ShowDialog();
        private void CashboxButton_Click(object sender, RoutedEventArgs e) { if (_currentShift == null) { MessageBox.Show("Откройте смену!", "Внимание"); return; } CashboxPanel.Show(_currentShift); }
        private void ServicesButton_Click(object sender, RoutedEventArgs e) => App.GetService<ServiceManagementWindow>().ShowDialog();
        private void ReportsButton_Click(object sender, RoutedEventArgs e) => new ReportsWindow(_currentUser).ShowDialog();
        private void ExitButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        private void ClientsButton_Click(object sender, RoutedEventArgs e) => App.GetService<ClientsWindow>().ShowDialog();
        private void HistoryButton_Click(object sender, RoutedEventArgs e) => new HistoryWindow(_currentUser).ShowDialog();
        private void AppointmentButton_Click(object sender, RoutedEventArgs e) { var win = App.GetService<AppointmentWindow>(); win.Closed += (s, args) => _ = LoadDataAsync(); win.ShowDialog(); }
        private void ViewAppointmentsButton_Click(object sender, RoutedEventArgs e)
        {
            if (AppointmentsOverlay != null)
            {
                // Отписываемся от старых подписок (чтобы не дублировать)
                AppointmentsOverlay.OnEditRequested -= OpenEditOrder;
                // Подписываемся
                AppointmentsOverlay.OnEditRequested += OpenEditOrder;

                System.Diagnostics.Debug.WriteLine($"[DEBUG] ✅ Подписка OnEditRequested установлена");

                AppointmentsOverlay.Show();
            }
        }
        private void AppointmentsBoardButton_Click(object sender, RoutedEventArgs e) => AppointmentsOverlay?.Show();
        private void RefreshButton_Click(object sender, RoutedEventArgs e) => _ = LoadDataAsync();

        private void EditOrderMenuItem_Click(object sender, RoutedEventArgs e) { if (SelectedItem != null) OpenEditOrder(SelectedItem); else MessageBox.Show("Выберите заказ"); }



        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show("Выгрузить данные с сервера в JSON?", "Экспорт", MessageBoxButton.YesNo);
                if (result != MessageBoxResult.Yes) return;

                var users = await _apiService.GetUsersAsync();
                var services = await _apiService.GetServicesAsync();
                var clients = await _apiService.GetClientsAsync();
                var shifts = await _apiService.GetShiftsAsync();

                var exportData = new { Users = users, Services = services, Clients = clients, Shifts = shifts };

                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string folder = Path.Combine(docs, "MyCarWashing", "Exports");
                Directory.CreateDirectory(folder);

                string file = Path.Combine(folder, $"export_api_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                File.WriteAllText(file, Newtonsoft.Json.JsonConvert.SerializeObject(exportData, Newtonsoft.Json.Formatting.Indented));

                MessageBox.Show($"Данные экспортированы!\n\n📁 Файл сохранен в:\n{file}", "Успешно");
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка экспорта: {ex.Message}"); }
        }

        private async void StartShiftButton_Click(object sender, RoutedEventArgs e)
        {
            var startShiftWin = new StartShiftWindow();
            if (startShiftWin.ShowDialog() == true) await LoadDataAsync();
        }

        private async void CloseShiftButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentShift == null || _currentShift.IsClosed)
            {
                MessageBox.Show("Нет смены для закрытия");
                return;
            }

            //  ДОБАВЛЯЕМ ПРОВЕРКУ НА АКТИВНЫЕ ЗАКАЗЫ 
            bool hasActiveOrders = _allOrders.Any(o => o.Status == "Выполняется" || o.Status == "В работе");
            if (hasActiveOrders)
            {
                MessageBox.Show("Нельзя закрыть смену! Завершите или отмените все активные заказы.",
                                "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Закрыть смену?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    await _apiService.CloseShiftAsync(_currentShift.Id);
                    await LoadDataAsync();
                    MessageBox.Show("Смена успешно закрыта!", "Успешно");
                }
                catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
            }
        }

        private void BoxItemsControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is OrderDisplayItem selected) OpenEditOrder(selected);
        }

        private async void InitializeSignalR()
        {
            // Строим подключение (порт берем из твоих логов API)
            _hubConnection = new HubConnectionBuilder()
                .WithUrl("https://localhost:7165/hubs/app")
                .WithAutomaticReconnect() // Автоматически переподключаться при обрыве сети
                .Build();

            // Подписываемся на событие "UpdateData", которое мы отправляем с сервера
            _hubConnection.On("UpdateData", () =>
            {
                // ВАЖНО: SignalR работает в фоновом потоке. 
                // Чтобы обновить UI (WPF), нужно перебросить вызов в главный поток интерфейса.
                Application.Current.Dispatcher.Invoke(() =>
                {
                    RefreshData(); // Вызываем твой метод перезагрузки данных
                });
            });

            try
            {
                // Запускаем слушателя
                await _hubConnection.StartAsync();
                System.Diagnostics.Debug.WriteLine("SignalR Connected!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SignalR Connection Error: {ex.Message}");
            }
        }
    }

    // ==========================================
    // ВСПОМОГАТЕЛЬНЫЕ КЛАССЫ (ОБЯЗАТЕЛЬНО НУЖНЫ)
    // ==========================================

    public class WasherStat
    {
        public string WasherName { get; set; }
        public int CarsCount { get; set; }
        public decimal Earnings { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal Percentage { get; set; }
    }
}