// Подключение пространств имен и создание явных алиасов для разрешения конфликтов имен
using AccuratPanelCarWashing.Controls;
using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using AccuratPanelCarWashing.ViewModels;
using AccuratSystem.Contracts.Models;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ContractsBranch = AccuratSystem.Contracts.Models.Branch;
using ContractsClient = AccuratSystem.Contracts.Models.Client;
using ContractsOrder = AccuratSystem.Contracts.Models.Order;
using ContractsOrderWasher = AccuratSystem.Contracts.Models.OrderWasher;
using ContractsService = AccuratSystem.Contracts.Models.Service;
using ContractsShift = AccuratSystem.Contracts.Models.Shift;
using ContractsTransaction = AccuratSystem.Contracts.Models.Transaction;
using ContractsUser = AccuratSystem.Contracts.Models.User;
using WpfAppointment = AccuratPanelCarWashing.Models.Appointment;
using WpfUser = AccuratPanelCarWashing.Models.User;

namespace AccuratPanelCarWashing
{
    /// <summary>
    /// Главное окно приложения, реализующее логику отображения заказов, управления сменами и взаимодействия с сервером.
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region События

        /// <summary>
        /// Событие, возникающее при изменении значения свойства для уведомления пользовательского интерфейса.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Приватные поля

        /// <summary>
        /// Сервис для взаимодействия с API сервера.
        /// </summary>
        private ApiService _apiService;

        /// <summary>
        /// Локальный кэш всех заказов, полученных с сервера.
        /// </summary>
        private List<ContractsOrder> _allOrders = new List<ContractsOrder>();

        /// <summary>
        /// Список предварительных записей на текущий день.
        /// </summary>
        private List<WpfAppointment> _todayAppointments = new List<WpfAppointment>();

        /// <summary>
        /// Текущая активная рабочая смена.
        /// </summary>
        private ContractsShift _currentShift;

        /// <summary>
        /// Идентификатор текущего выбранного филиала.
        /// </summary>
        private int _currentBranchId;

        /// <summary>
        /// Текущий авторизованный пользователь системы.
        /// </summary>
        private WpfUser _currentUser;

        /// <summary>
        /// Текущая строка поискового фильтра.
        /// </summary>
        private string _searchFilter = "";

        /// <summary>
        /// Таймер для обновления пользовательского интерфейса.
        /// </summary>
        private System.Windows.Threading.DispatcherTimer _uiTimer;

        /// <summary>
        /// Подключение к хабу SignalR для получения уведомлений в реальном времени.
        /// </summary>
        private HubConnection _hubConnection;

        /// <summary>
        /// Локальный кэш всех рабочих смен.
        /// </summary>
        private List<ContractsShift> _allShiftsCache = new List<ContractsShift>();

        /// <summary>
        /// Флаг, указывающий на то, что в данный момент выполняется загрузка данных.
        /// </summary>
        private bool _isDataLoading = false;

        /// <summary>
        /// Локальный кэш доступных услуг.
        /// </summary>
        private List<ContractsService> _cachedServices = new List<ContractsService>();

        /// <summary>
        /// Локальный кэш пользователей системы.
        /// </summary>
        private List<ContractsUser> _cachedUsers = new List<ContractsUser>();

        /// <summary>
        /// Статистика работы мойщиков.
        /// </summary>
        private List<WasherStat> _washersStats;

        /// <summary>
        /// Прибыль компании за текущую смену.
        /// </summary>
        private decimal _companyEarnings;

        /// <summary>
        /// Общая выручка за текущую смену.
        /// </summary>
        private decimal _totalRevenue;

        /// <summary>
        /// Коллекция вкладок филиалов.
        /// </summary>
        private ObservableCollection<BranchTabItem> _branchTabs = new ObservableCollection<BranchTabItem>();

        /// <summary>
        /// Текущая выбранная вкладка филиала.
        /// </summary>
        private BranchTabItem _selectedBranchTab;

        /// <summary>
        /// Текущий выбранный элемент заказа в интерфейсе.
        /// </summary>
        private OrderDisplayItem _selectedItem;

        #endregion

        #region Публичные свойства и поля для привязки данных

        /// <summary>
        /// Признак того, что текущий пользователь является директором.
        /// </summary>
        public bool IsDirector => UserPermissions.IsSuperUser(_currentUser);

        /// <summary>
        /// Признак того, что пользователь привязан только к одному филиалу.
        /// </summary>
        public bool IsSingleBranch => _currentUser?.RoleId != 1;

        /// <summary>
        /// Признак того, что текущий пользователь является администратором или директором.
        /// </summary>
        public bool IsAdminOrDirector => UserPermissions.IsManagement(_currentUser);

        /// <summary>
        /// Коллекция вкладок филиалов для отображения в интерфейсе.
        /// </summary>
        public ObservableCollection<BranchTabItem> BranchTabs
        {
            get => _branchTabs;
            set { _branchTabs = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BranchTabs))); }
        }

        /// <summary>
        /// Выбранная вкладка филиала с логикой переключения и обновления данных.
        /// </summary>
        public BranchTabItem SelectedBranchTab
        {
            get => _selectedBranchTab;
            set
            {
                if (_selectedBranchTab != value)
                {
                    _selectedBranchTab = value;

                    // Если переключили вкладку и сейчас НЕ идет загрузка данных из БД:
                    if (_selectedBranchTab != null && !_isDataLoading)
                    {
                        _currentBranchId = _selectedBranchTab.BranchId;
                        AppSettings.CurrentBranchId = _currentBranchId; // Синхронизируем с AppSettings

                        // Берем смену из кэша мгновенно, без запроса к серверу
                        _currentShift = _allShiftsCache.FirstOrDefault(s => !s.IsClosed && s.BranchId == _currentBranchId);

                        ApplyFilterAndDisplay();
                        UpdateInfo();
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedBranchTab)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentBranchDisplay)));
                }
            }
        }

        /// <summary>
        /// Отображаемое название текущего выбранного филиала.
        /// </summary>
        public string CurrentBranchDisplay => SelectedBranchTab != null ? $"{SelectedBranchTab.BranchName}" : "";

        /// <summary>
        /// Информация об активном пользователе для отображения в интерфейсе.
        /// </summary>
        public string ActiveUserInfo
        {
            get
            {
                if (_currentUser == null) return "Гость";
                return $"{_currentUser.FullName} • {_currentUser.RoleDisplay}";
            }
        }

        /// <summary>
        /// Информация о текущей смене.
        /// </summary>
        public string CurrentShiftInfo { get; private set; }

        /// <summary>
        /// Сводная информация о количестве заказов и финансовых показателях.
        /// </summary>
        public string TotalOrdersInfo { get; private set; }

        /// <summary>
        /// Выбранный заказ с логикой обновления состояния выделения.
        /// </summary>
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

        /// <summary>
        /// Количество оплат посредством QR-кода.
        /// </summary>
        public int QrCount { get; set; }

        /// <summary>
        /// Сумма оплат посредством QR-кода.
        /// </summary>
        public decimal QrAmount { get; set; }

        /// <summary>
        /// Количество оплат наличными средствами.
        /// </summary>
        public int CashCount { get; set; }

        /// <summary>
        /// Сумма оплат наличными средствами.
        /// </summary>
        public decimal CashAmount { get; set; }

        /// <summary>
        /// Количество оплат банковской картой.
        /// </summary>
        public int CardCount { get; set; }

        /// <summary>
        /// Сумма оплат банковской картой.
        /// </summary>
        public decimal CardAmount { get; set; }

        /// <summary>
        /// Количество оплат переводом.
        /// </summary>
        public int TransferCount { get; set; }

        /// <summary>
        /// Сумма оплат переводом.
        /// </summary>
        public decimal TransferAmount { get; set; }

        /// <summary>
        /// Статистика работы мойщиков для привязки к интерфейсу.
        /// </summary>
        public List<WasherStat> WashersStats { get => _washersStats; set { _washersStats = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WashersStats))); } }

        /// <summary>
        /// Прибыль компании для привязки к интерфейсу.
        /// </summary>
        public decimal CompanyEarnings { get => _companyEarnings; set { _companyEarnings = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CompanyEarnings))); } }

        /// <summary>
        /// Общая выручка для привязки к интерфейсу.
        /// </summary>
        public decimal TotalRevenue { get => _totalRevenue; set { _totalRevenue = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalRevenue))); } }

        /// <summary>
        /// Признак того, что текущий пользователь является разработчиком.
        /// </summary>
        public bool IsDeveloper => _currentUser?.RoleId == 0 || _currentUser?.Role?.Name == "Разработчик";

        #endregion

        #region Конструктор и инициализация

        /// <summary>
        /// Инициализирует новый экземпляр главного окна и настраивает начальные параметры.
        /// </summary>
        public MainWindow(WpfUser user)
        {
            InitializeComponent();
            _apiService = new ApiService();
            _currentUser = user;

            this.Closed += (s, e) => Application.Current.Shutdown();

            App.CurrentUser = user;
            DataContext = this;

            _currentBranchId = AppSettings.CurrentBranchId;
            AppointmentsOverlay.OnEditRequested += OpenEditOrder;

            this.Loaded += MainWindow_Loaded;
            InitializeSignalR();
            InitializeTimer();
        }

        #endregion

        #region Жизненный цикл окна и загрузка данных

        /// <summary>
        /// Обработчик события загрузки окна, запускающий асинхронную загрузку данных.
        /// </summary>
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Теперь загрузка данных начнется только тогда, когда окно реально появится на экране
            await LoadDataAsync();
        }

        /// <summary>
        /// Устанавливает нового пользователя и инициирует перезагрузку данных.
        /// </summary>
        public void SetUser(WpfUser user)
        {
            _currentUser = user;
            _ = LoadDataAsync();
        }

        /// <summary>
        /// Инициирует принудительное обновление данных из источника.
        /// </summary>
        public void RefreshData() => _ = LoadDataAsync();

        /// <summary>
        /// Асинхронно загружает все необходимые данные с сервера и обновляет локальный кэш.
        /// </summary>
        private async Task LoadDataAsync()
        {
            if (_isDataLoading) return; // Защита от бесконечного цикла
            _isDataLoading = true;

            try
            {
                // Запоминаем филиал ДО обновления
                int savedBranchId = _currentBranchId > 0 ? _currentBranchId : AppSettings.CurrentBranchId;

                var allBranches = await _apiService.GetBranchesAsync();
                _allShiftsCache = await _apiService.GetShiftsAsync();
                _cachedUsers = await _apiService.GetUsersAsync();
                _cachedServices = await _apiService.GetServicesAsync();
                var allOrdersFromApi = await _apiService.GetOrdersAsync();

                //  Просто сохраняем ВСЕ заказы с сервера в локальный кэш!
                // Нам не нужно их тут жестко резать, так как методы ApplyFilterAndDisplay() 
                // и UpdateInfo() сами прекрасно всё отфильтруют по нужной смене и филиалу.
                _allOrders = allOrdersFromApi;

                var newTabs = new ObservableCollection<BranchTabItem>();

                if (IsAdminOrDirector)
                {
                    foreach (var b in allBranches)
                    {
                        var tab = new BranchTabItem { BranchId = b.Id, BranchName = b.Name };
                        PopulateZones(tab, b);
                        newTabs.Add(tab);
                    }
                }
                else
                {
                    var myBranch = allBranches.FirstOrDefault(b => b.Id == savedBranchId);
                    if (myBranch != null)
                    {
                        var tab = new BranchTabItem { BranchId = myBranch.Id, BranchName = myBranch.Name };
                        PopulateZones(tab, myBranch);
                        newTabs.Add(tab);
                    }
                }

                BranchTabs = newTabs;

                // ВОССТАНАВЛИВАЕМ вкладку
                if (BranchTabs.Any())
                {
                    var tabToSelect = BranchTabs.FirstOrDefault(t => t.BranchId == savedBranchId) ?? BranchTabs.First();
                    _selectedBranchTab = tabToSelect;
                    _currentBranchId = tabToSelect.BranchId;
                    AppSettings.CurrentBranchId = _currentBranchId;

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedBranchTab)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentBranchDisplay)));
                }

                // ТОЛЬКО ТЕПЕРЬ мы знаем правильный филиал и можем найти активную смену
                _currentShift = _allShiftsCache.FirstOrDefault(s => !s.IsClosed && s.BranchId == _currentBranchId);

                // Теперь методы вытащат из полного кэша _allOrders то, что нужно
                ApplyFilterAndDisplay();
                UpdateInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки данных: " + ex.Message);
            }
            finally
            {
                _isDataLoading = false; // Снимаем блокировку
            }
        }

        #endregion

        #region Логика фильтрации и отображения данных

        /// <summary>
        /// Заполняет рабочие зоны (боксы и подъемники) для указанной вкладки филиала.
        /// </summary>
        private void PopulateZones(BranchTabItem tab, ContractsBranch branch)
        {
            for (int i = 1; i <= branch.WashBaysCount; i++)
                tab.WashZones.Add(new WorkZone { ZoneNumber = i, ZoneName = $"🚿 БОКС {i}", Department = "Wash" });

            for (int i = 1; i <= branch.ServiceLiftsCount; i++)
                tab.ServiceZones.Add(new WorkZone { ZoneNumber = i, ZoneName = $"🔧 ПОДЪЕМНИК {i}", Department = "Service" });
        }

        /// <summary>
        /// Применяет фильтры к списку заказов и распределяет их по рабочим зонам для отображения.
        /// </summary>
        private void ApplyFilterAndDisplay()
        {
            int currentShiftId = _currentShift?.Id ?? -1;

            // ИСПРАВЛЕНИЕ ФИЛЬТРА: Расширили условие.
            // Теперь мы железно показываем ВСЕ обычные заказы за сегодня, даже если со сменой что-то не так.
            var filteredOrders = _allOrders.Where(o =>
                o.BranchId == _currentBranchId &&
                (
                    // 1. Обычные заказы: привязаны к смене ИЛИ созданы сегодня (надежная страховка)
                    (!o.IsAppointment && (o.ShiftId == currentShiftId || o.Time.Date == DateTime.Now.Date)) ||

                    // 2. Предварительные записи: от сегодня и в будущее
                    (o.IsAppointment && o.Time >= DateTime.Now.Date &&
                     (o.Status == "Предварительная запись" || o.Status == "Запись" || o.Status == "Ожидает"))
                )
            ).AsEnumerable();

            // 2. Локальный поиск по номеру/модели
            if (!string.IsNullOrWhiteSpace(_searchFilter))
            {
                string filter = _searchFilter.ToLower();
                filteredOrders = filteredOrders.Where(o =>
                    (o.CarNumber != null && o.CarNumber.ToLower().Contains(filter)) ||
                    (o.CarModel != null && o.CarModel.ToLower().Contains(filter)));
            }

            // 3. Формируем элементы для отрисовки (разделяя заказы и записи)
            var orderItems = filteredOrders.Where(o => !o.IsAppointment).Select(o => new OrderDisplayItem
            {
                Id = o.Id,
                BranchId = o.BranchId,
                Department = o.Department,
                CarModel = o.CarModel,
                CarNumber = o.CarNumber,
                Time = o.Time,
                WasherName = GetWasherName(o.GetWasherId()),
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
                StatusStartTime = o.CurrentStatusStartTime, // Передаем время из API в UI-модель
            });

            var appointmentItems = filteredOrders.Where(o => o.IsAppointment).Select(o => new OrderDisplayItem
            {
                Id = o.Id,
                BranchId = o.BranchId,
                Department = o.Department,
                CarModel = o.CarModel,
                CarNumber = o.CarNumber,
                Time = o.Time,
                WasherName = o.GetWasherId() > 0 ? GetWasherName(o.GetWasherId()) : "📅 Запись",
                ServicesList = string.Join(", ", (o.ServiceIds ?? new List<int>()).Select(id => _cachedServices.FirstOrDefault(s => s.Id == id)?.Name ?? "Unknown")),
                FinalPrice = o.FinalPrice,
                ExtraCost = o.ExtraCost,
                ExtraCostReason = o.ExtraCostReason,
                BoxNumber = o.BoxNumber,
                Status = o.Status,
                IsAppointment = true,
                IsCompleted = false,
                PaymentMethod = o.PaymentMethod,
            });

            var allDisplayItems = orderItems.Concat(appointmentItems).OrderBy(i => i.Time).ToList();

            // 4. Раскидываем по боксам
            foreach (var tab in BranchTabs)
            {
                foreach (var zone in tab.WashZones.Concat(tab.ServiceZones))
                {
                    var ordersForZone = allDisplayItems.Where(i =>
                        i.BranchId == tab.BranchId &&
                        i.BoxNumber == zone.ZoneNumber &&
                        i.Department == zone.Department).ToList();

                    zone.Orders.Clear();
                    foreach (var o in ordersForZone) zone.Orders.Add(o);
                }
            }
        }

        #endregion

        #region Вспомогательные методы

        /// <summary>
        /// Возвращает полное имя мойщика по его идентификатору.
        /// </summary>
        private string GetWasherName(int? washerId)
        {
            if (washerId == null || washerId == 0) return "Не назначен";
            var washer = _cachedUsers.FirstOrDefault(u => u.Id == washerId);
            return washer?.FullName ?? "Не назначен";
        }

        #endregion

        #region Обновление информации и статистики

        /// <summary>
        /// Обновляет сводную информацию о текущей смене, выручке и количестве заказов.
        /// </summary>
        private void UpdateInfo()
        {
            int currentShiftId = _currentShift?.Id ?? -1;

            // Фильтруем заказы ТОЛЬКО для текущего филиала И ТЕКУЩЕЙ СМЕНЫ
            var branchOrders = _allOrders.Where(o =>
                o.BranchId == _currentBranchId &&
                !o.IsAppointment &&
                o.ShiftId == currentShiftId).ToList();

            var activeServiceCount = branchOrders
                .Count(o => o.Department == "Service" && o.Status == "В работе");

            if (activeServiceCount > 0)
            {
                TotalOrdersInfo += $" | 🔧 Сервис в работе: {activeServiceCount}";
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalOrdersInfo)));
            }

            if (_currentShift != null && !_currentShift.IsClosed)
            {
                CurrentShiftInfo = $"📅 Смена: {_currentShift.Date:dd.MM.yyyy} | Начало: {_currentShift.StartTime:HH:mm}";

                var completedOrders = branchOrders.Where(o => o.Status == "Выполнен" || o.Status == "Завершен").ToList();
                TotalRevenue = completedOrders.Sum(o => o.FinalPrice);

                var totalWasherEarnings = completedOrders.Sum(o => OrderMath.Calculate(o, _cachedServices, _cachedUsers, null).WasherEarnings);

                // Считаем только СВОИ бонусы из заказов, которые создал Я
                decimal myUpsellShare = completedOrders
                    .Where(o => o.AdminId == _currentUser?.Id)
                    .Sum(o => OrderMath.ExtractUpsellBonus(o.Notes));

                // Считаем ВСЕ бонусы за смену (чтобы вычесть их из прибыли компании)
                decimal totalShiftUpsellBonuses = completedOrders.Sum(o => OrderMath.ExtractUpsellBonus(o.Notes));

                decimal adminPercent = _currentUser?.BaseWagePercentage ?? 0;
                var adminShiftPercentEarnings = TotalRevenue * (adminPercent / 100m);

                // ЗП именно того админа, который сейчас смотрит в экран
                var myTotalEarnings = adminShiftPercentEarnings + myUpsellShare;

                // Из прибыли компании вычитаем процент админа и ВСЕ выплаченные за смену бонусы кассиров
                CompanyEarnings = completedOrders.Sum(o => OrderMath.Calculate(o, _cachedServices, _cachedUsers, null).CompanyEarnings) - adminShiftPercentEarnings - totalShiftUpsellBonuses;

                var inProgressCount = branchOrders.Count(o => o.Status == "В работе");
                var cancelledCount = branchOrders.Count(o => o.Status == "Отменен");

                TotalOrdersInfo = $"🚗 Выполнено: {completedOrders.Count} | 🟢 В работе: {inProgressCount} | 👤 Мойщикам: {totalWasherEarnings:N0} ₽ | 💰 Мне: {myTotalEarnings:N0} ₽ (из них апселл: {myUpsellShare:N0} ₽)";
            }
            else if (_currentShift != null && _currentShift.IsClosed)
            {
                CurrentShiftInfo = $"📅 Смена закрыта: {_currentShift.Date:dd.MM.yyyy}";
                var completedOrders = branchOrders.Where(o => o.Status == "Выполнен" || o.Status == "Завершен").ToList();
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

            UpdateWashersStats(branchOrders);
            UpdatePaymentStats(branchOrders);

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentShiftInfo)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalOrdersInfo)));
        }

        /// <summary>
        /// Обновляет статистику оплат по различным методам оплаты.
        /// </summary>
        private void UpdatePaymentStats(List<ContractsOrder> branchOrders)
        {
            if (!branchOrders.Any())
            {
                CashCount = 0; CashAmount = 0; CardCount = 0; CardAmount = 0;
                TransferCount = 0; TransferAmount = 0; QrCount = 0; QrAmount = 0; return;
            }

            CashCount = branchOrders.Count(o => o.PaymentMethod == "Наличные");
            CashAmount = branchOrders.Where(o => o.PaymentMethod == "Наличные").Sum(o => o.FinalPrice);
            CardCount = branchOrders.Count(o => o.PaymentMethod == "Карта");
            CardAmount = branchOrders.Where(o => o.PaymentMethod == "Карта").Sum(o => o.FinalPrice);
            TransferCount = branchOrders.Count(o => o.PaymentMethod == "Перевод");
            TransferAmount = branchOrders.Where(o => o.PaymentMethod == "Перевод").Sum(o => o.FinalPrice);
            QrCount = branchOrders.Count(o => o.PaymentMethod == "QR-код");
            QrAmount = branchOrders.Where(o => o.PaymentMethod == "QR-код").Sum(o => o.FinalPrice);

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CashCount)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CashAmount)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CardCount)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CardAmount)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TransferCount)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TransferAmount)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QrCount)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QrAmount)));
        }

        /// <summary>
        /// Обновляет статистику производительности и заработка мойщиков за текущую смену.
        /// </summary>
        private void UpdateWashersStats(List<ContractsOrder> branchOrders)
        {
            if (_currentShift == null || _currentShift.IsClosed || !branchOrders.Any())
            {
                WashersStats = new List<WasherStat>();
                return;
            }

            var completedOrders = branchOrders.Where(o => o.Status == "Выполнен").ToList();
            var totalShiftRevenue = completedOrders.Sum(o => OrderMath.Calculate(o, _cachedServices).FinalPrice);

            WashersStats = completedOrders.Where(o => o.GetWasherId() > 0).GroupBy(o => o.GetWasherId()).Select(g =>
            {
                var washerRevenue = g.Sum(o => OrderMath.Calculate(o, _cachedServices, _cachedUsers, null).FinalPrice);
                return new WasherStat
                {
                    WasherName = GetWasherName(g.Key),
                    CarsCount = g.Count(),
                    Earnings = g.Sum(o => OrderMath.Calculate(o, _cachedServices, _cachedUsers, null).WasherEarnings),
                    TotalRevenue = washerRevenue,
                    Percentage = totalShiftRevenue > 0 ? (washerRevenue / totalShiftRevenue) * 100m : 0m
                };
            }).OrderByDescending(s => s.Earnings).ToList();
        }

        #endregion

        #region Обработчики событий элементов управления

        /// <summary>
        /// Обрабатывает получение фокуса полем поиска, очищая текст-подсказку.
        /// </summary>
        private void SearchFilterTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchFilterTextBox.Text == "🔍 Поиск по гос. номеру или модели...")
            {
                SearchFilterTextBox.Text = "";
                SearchFilterTextBox.Foreground = Brushes.Black;
            }
        }

        /// <summary>
        /// Обрабатывает потерю фокуса полем поиска, восстанавливая текст-подсказку при пустом вводе.
        /// </summary>
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

        /// <summary>
        /// Обрабатывает нажатие клавиш в поле поиска, применяя фильтр к списку заказов.
        /// </summary>
        private void SearchFilterTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            _searchFilter = SearchFilterTextBox.Text.Trim();
            if (_searchFilter == "🔍 Поиск по гос. номеру или модели...") _searchFilter = "";
            ApplyFilterAndDisplay();
        }

        /// <summary>
        /// Обрабатывает двойной щелчок по элементу списка заказов, открывая окно редактирования.
        /// </summary>
        private void BoxItemsControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is OrderDisplayItem selected) OpenEditOrder(selected);
        }

        #endregion

        #region Работа с заказами

        /// <summary>
        /// Открывает окно добавления нового заказа.
        /// </summary>
        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentShift == null || _currentShift.IsClosed)
            {
                MessageBox.Show("Сначала начните смену!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var orderViewModel = App.GetService<AddEditOrderViewModel>();
            var addWin = new AddEditOrderWindow(orderViewModel, _currentShift);

            // ВАЖНО: передаем выбранный филиал принудительно, чтобы отрисовались правильные боксы
            orderViewModel.CurrentOrder.BranchId = _currentBranchId;

            if (addWin.ShowDialog() == true) _ = LoadDataAsync();
        }

        /// <summary>
        /// Открывает окно редактирования существующего заказа.
        /// </summary>
        private void OpenEditOrder(OrderDisplayItem orderDisplay)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] OpenEditOrder: orderDisplay.Id={orderDisplay.Id}");

            var originalOrder = _allOrders.FirstOrDefault(o => o.Id == orderDisplay.Id);

            if (originalOrder == null && orderDisplay.Id > 0)
            {
                MessageBox.Show($"Заказ #{orderDisplay.Id} не найден в кэше.\nПопробуйте обновить данные (кнопка 🔄).", "Предупреждение");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[DEBUG] originalOrder найден: Id={originalOrder?.Id}, CarNumber={originalOrder?.CarNumber}");

            if (originalOrder != null)
            {
                var orderViewModel = App.GetService<AddEditOrderViewModel>();
                var orderEditWin = new AddEditOrderWindow(orderViewModel, _currentShift, originalOrder);
                if (orderEditWin.ShowDialog() == true) _ = LoadDataAsync();
            }
        }

        /// <summary>
        /// Обрабатывает выбор пункта меню удаления (отмены) заказа.
        /// </summary>
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

        /// <summary>
        /// Обрабатывает выбор пункта меню редактирования заказа.
        /// </summary>
        private void EditOrderMenuItem_Click(object sender, RoutedEventArgs e) { if (SelectedItem != null) OpenEditOrder(SelectedItem); else MessageBox.Show("Выберите заказ"); }

        #endregion

        #region Обработчики кнопок и навигация

        /// <summary>
        /// Открывает окно управления карточками сотрудников.
        /// </summary>
        private void EmployeesButton_Click(object sender, RoutedEventArgs e) => App.GetService<EmployeeCardWindow>().ShowDialog();

        /// <summary>
        /// Открывает панель управления кассой для текущей смены.
        /// </summary>
        private void CashboxButton_Click(object sender, RoutedEventArgs e) { if (_currentShift == null) { MessageBox.Show("Откройте смену!", "Внимание"); return; } CashboxPanel.Show(_currentShift); }

        /// <summary>
        /// Открывает окно управления списком услуг.
        /// </summary>
        private void ServicesButton_Click(object sender, RoutedEventArgs e)
        {
            // if (!UserSession.IsFeatureEnabled(f => f.IsServicesEnabled)) // Например, привязали к модулю Склад
            // {
            //     MessageBox.Show("Модуль управления услугами заблокирован 🔒");
            //     return;
            // }
            App.GetService<ServiceManagementWindow>().ShowDialog();
        }

        /// <summary>
        /// Открывает окно формирования и просмотра отчетов.
        /// </summary>
        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем, включен ли модуль CRM/Маркетинга (как пример для отчетов)
            // if (!UserSession.IsFeatureEnabled(f => f.IsCrmMarketingEnabled))
            // {
            //     MessageBox.Show("Этот модуль (Продвинутая аналитика) недоступен для вашего филиала.\n\nСвяжитесь с администратором для активации 🔒", "Модуль заблокирован");
            //     return;
            // }

            new ReportsWindow(_currentUser).ShowDialog();
        }

        /// <summary>
        /// Завершает работу приложения.
        /// </summary>
        private void ExitButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        /// <summary>
        /// Открывает окно управления базой клиентов.
        /// </summary>
        private void ClientsButton_Click(object sender, RoutedEventArgs e) => App.GetService<ClientsWindow>().ShowDialog();

        /// <summary>
        /// Открывает окно просмотра истории операций.
        /// </summary>
        private void HistoryButton_Click(object sender, RoutedEventArgs e) => new HistoryWindow(_currentUser).ShowDialog();

        /// <summary>
        /// Отображает оверлей с доской предварительных записей.
        /// </summary>
        private void AppointmentsBoardButton_Click(object sender, RoutedEventArgs e) => AppointmentsOverlay?.Show();

        /// <summary>
        /// Инициирует ручное обновление данных при нажатии кнопки обновления.
        /// </summary>
        private void RefreshButton_Click(object sender, RoutedEventArgs e) => _ = LoadDataAsync();

        /// <summary>
        /// Экспортирует данные с сервера в локальный JSON-файл.
        /// </summary>
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
                System.IO.File.WriteAllText(file, Newtonsoft.Json.JsonConvert.SerializeObject(exportData, Newtonsoft.Json.Formatting.Indented));

                MessageBox.Show($"Данные экспортированы!\n\n📁 Файл сохранен в:\n{file}", "Успешно");
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка экспорта: {ex.Message}"); }
        }

        /// <summary>
        /// Открывает окно запуска новой рабочей смены.
        /// </summary>
        private async void StartShiftButton_Click(object sender, RoutedEventArgs e)
        {
            var startShiftWin = new StartShiftWindow();
            startShiftWin.PreselectedBranchId = _currentBranchId;

            if (startShiftWin.ShowDialog() == true)
                await LoadDataAsync();
        }

        /// <summary>
        /// Выполняет процедуру закрытия текущей рабочей смены с проверкой активных заказов.
        /// </summary>
        private async void CloseShiftButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentShift == null || _currentShift.IsClosed)
            {
                MessageBox.Show("Нет смены для закрытия");
                return;
            }

            // НОВАЯ ЛОГИКА: разделяем проверку по департаментам
            var branchOrders = _allOrders.Where(o => o.BranchId == _currentBranchId).ToList();

            // Мойка: блокируем, если есть активные заказы
            var activeWashOrders = branchOrders
                .Where(o => o.Department == "Wash" && o.Status == "В работе")
                .ToList();

            if (activeWashOrders.Any())
            {
                MessageBox.Show(
                    $"Нельзя закрыть смену! В мойке есть активные заказы:\n" +
                    $"{string.Join(", ", activeWashOrders.Select(o => o.CarNumber))}\n\n" +
                    $"Завершите или отмените их перед закрытием.",
                    "Внимание",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // ⚠️ Сервис: только предупреждаем, но не блокируем
            var activeServiceOrders = branchOrders
                .Where(o => o.Department == "Service" && o.Status == "В работе")
                .ToList();

            if (activeServiceOrders.Any())
            {
                var result = MessageBox.Show(
                    $"В сервисе остались активные заказы:\n" +
                    $"{string.Join(", ", activeServiceOrders.Select(o => $"{o.CarNumber} ({o.CarModel})"))}\n\n" +
                    $"Они автоматически перейдут в следующую смену этого филиала.\n" +
                    $"Продолжить закрытие смены?",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result != MessageBoxResult.Yes)
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

        /// <summary>
        /// Открывает окно управления модулем дополнительных продаж.
        /// </summary>
        private void UpsellButton_Click(object sender, RoutedEventArgs e)
        {
            // Добавили проверку купленного модуля
            if (!UserSession.IsFeatureEnabled(f => f.IsUpsellEnabled))
            {
                MessageBox.Show("Модуль 'Умный кассир' отключен для вашей компании 🔒\nСвяжитесь с поддержкой для приобретения.", "Доступ закрыт");
                return;
            }

            new UpsellManagementWindow().ShowDialog();
        }

        /// <summary>
        /// Открывает окно авторизации для смены текущего пользователя.
        /// </summary>
        private void ChangeUserButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Получаем окно логина через ваш DI-сервис (как в App.xaml.cs)
            var loginWin = App.GetService<LoginWindow>();

            // 2. Показываем его как диалоговое окно
            if (loginWin.ShowDialog() == true)
            {
                var newUser = loginWin.AuthenticatedUser;

                if (newUser != null)
                {
                    // 3. Обновляем пользователя
                    // Метод SetUser внутри себя уже вызывает LoadDataAsync(), 
                    // поэтому повторно вызывать его здесь не нужно.
                    SetUser(newUser);

                    // 4. Оповещаем интерфейс, что права доступа изменились
                    // Это обновит видимость кнопок (Админ/Директор)
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveUserInfo)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAdminOrDirector)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDirector)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDeveloper)));

                    MessageBox.Show($"Пользователь успешно сменен на {newUser.FullName}", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        /// <summary>
        /// Открывает секретную панель разработчика.
        /// </summary>
        private void DevPanelButton_Click(object sender, RoutedEventArgs e)
        {
            // Открываем новую секретную панель
            new SuperAdminWindow(_currentUser).ShowDialog();
        }

        #endregion

        #region Real-time обновления и таймеры

        /// <summary>
        /// Инициализирует и настраивает подключение к SignalR для получения уведомлений об обновлениях.
        /// </summary>
        private async void InitializeSignalR()
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl("https://localhost:7165/hubs/app")
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On("UpdateData", () =>
            {
                Application.Current.Dispatcher.Invoke(() => { RefreshData(); });
            });

            try
            {
                _hubConnection.Reconnecting += error => { return Task.CompletedTask; };
                _hubConnection.Reconnected += connectionId =>
                {
                    Application.Current.Dispatcher.Invoke(() => { RefreshData(); });
                    return Task.CompletedTask;
                };
                _hubConnection.Closed += error => { return Task.CompletedTask; };

                await _hubConnection.StartAsync();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SignalR Connection Error: {ex.Message}"); }
        }

        /// <summary>
        /// Инициализирует таймер для посекундного обновления отображаемого времени нахождения заказа в статусе.
        /// </summary>
        private void InitializeTimer()
        {
            _uiTimer = new System.Windows.Threading.DispatcherTimer();
            _uiTimer.Interval = TimeSpan.FromSeconds(1); // Обновлять каждую секунду
            _uiTimer.Tick += (s, e) =>
            {
                // Проходим по всем вкладкам филиалов
                foreach (var tab in BranchTabs)
                {
                    // Проходим по всем зонам (и мойка, и сервис)
                    foreach (var zone in tab.WashZones.Concat(tab.ServiceZones))
                    {
                        foreach (var order in zone.Orders)
                        {
                            // Обновляем таймер только если заказ еще не завершен
                            if (!order.IsCompleted)
                            {
                                // ВАЖНО: вызываем обновление свойства TimeInStatus.
                                // Теперь WPF увидит, что время изменилось, и обновит текст на экране.
                                order.OnPropertyChanged(nameof(order.TimeInStatus));
                            }
                        }
                    }
                }
            };
            _uiTimer.Start();
        }

        #endregion

        #region Перетаскивание элементов (Drag and Drop)

        /// <summary>
        /// Инициирует процесс перетаскивания карточки заказа при зажатии левой кнопки мыши.
        /// </summary>
        private void OrderCard_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement fe)
            {
                var order = fe.DataContext as OrderDisplayItem;
                if (order != null)
                {
                    DataObject data = new DataObject("OrderItem", order);
                    DragDrop.DoDragDrop(this, data, DragDropEffects.Move);
                }
            }
        }

        /// <summary>
        /// Обрабатывает событие сброса карточки заказа в новую рабочую зону.
        /// </summary>
        private async void Order_Drop(object sender, DragEventArgs e)
        {
            var droppedOrder = e.Data.GetData("OrderItem") as OrderDisplayItem;
            if (droppedOrder == null) return;

            // Определяем, в какой бокс бросили (DataContext ListBox - это WorkZone)
            if (sender is ListBox lb && lb.DataContext is WorkZone targetZone)
            {
                // Визуальный перенос (мгновенный отклик)
                foreach (var tab in BranchTabs)
                {
                    foreach (var zone in tab.WashZones.Concat(tab.ServiceZones))
                    {
                        if (zone.Orders.Contains(droppedOrder))
                        {
                            zone.Orders.Remove(droppedOrder);
                            break;
                        }
                    }
                }
                targetZone.Orders.Add(droppedOrder);

                // Синхронизация с БД
                var originalOrder = _allOrders.FirstOrDefault(o => o.Id == droppedOrder.Id);
                if (originalOrder != null)
                {
                    originalOrder.BoxNumber = targetZone.ZoneNumber;
                    originalOrder.Department = targetZone.Department;
                    await _apiService.UpdateOrderAsync(originalOrder);
                }
            }
        }

        #endregion

        #region Обработка горячих клавиш

        /// <summary>
        /// Перехватывает нажатия клавиш на уровне окна для выполнения глобальных команд.
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Ctrl + N -> Добавить заказ
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)
            {
                AddButton_Click(this, null);
                e.Handled = true;
            }

            // F5 -> Обновить данные
            if (e.Key == Key.F5)
            {
                RefreshButton_Click(this, null);
                e.Handled = true;
            }

            // Ctrl + F -> Поиск (фокус в текстовое поле)
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
            {
                SearchFilterTextBox.Focus();
                e.Handled = true;
            }

            // Del -> Удалить выбранный заказ
            if (e.Key == Key.Delete && SelectedItem != null)
            {
                DeleteOrderMenuItem_Click(this, null);
                e.Handled = true;
            }
        }

        #endregion
    }

    #region Модели данных

    /// <summary>
    /// Модель данных для хранения статистики работы конкретного мойщика.
    /// </summary>
    public class WasherStat
    {
        /// <summary>
        /// Полное имя мойщика.
        /// </summary>
        public string WasherName { get; set; }

        /// <summary>
        /// Количество обслуженных автомобилей.
        /// </summary>
        public int CarsCount { get; set; }

        /// <summary>
        /// Заработная плата мойщика за смену.
        /// </summary>
        public decimal Earnings { get; set; }

        /// <summary>
        /// Общая выручка, принесенная мойщиком.
        /// </summary>
        public decimal TotalRevenue { get; set; }

        /// <summary>
        /// Доля выручки мойщика от общей выручки смены в процентах.
        /// </summary>
        public decimal Percentage { get; set; }
    }

    #endregion
}