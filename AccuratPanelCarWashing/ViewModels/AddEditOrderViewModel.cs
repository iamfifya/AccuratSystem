// === ЯВНЫЕ АЛИАСЫ ТОЛЬКО ДЛЯ КОНФЛИКТНЫХ ИМЁН ===
using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using AccuratSystem.Contracts.DTOs;
using AccuratSystem.Contracts.Enums;
using AccuratSystem.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ContractsClient = AccuratSystem.Contracts.Models.Client;
using ContractsOrder = AccuratSystem.Contracts.Models.Order;
using ContractsService = AccuratSystem.Contracts.Models.Service;
using ContractsShift = AccuratSystem.Contracts.Models.Shift;
using ContractsUser = AccuratSystem.Contracts.Models.User;

namespace AccuratPanelCarWashing.ViewModels
{
    public class AddEditOrderViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ContractsShift _currentShift;
        private ContractsOrder _existingOrder;
        private bool _isEditMode;
        private bool _isAppointment;
        private bool _isSubscribedToDataChanged = false;
        private OrderCalculation _currentCalc;
        private readonly ApiService _apiService;
        private List<ContractsService> _allServicesCache = new List<ContractsService>();
        private string _currentDepartment = "Wash";

        public string CurrentDepartment
        {
            get => _currentDepartment;
            set
            {
                if (_currentDepartment != value)
                {
                    _currentDepartment = value;
                    OnPropertyChanged(nameof(CurrentDepartment));
                    FilterServicesByDepartment();
                }
            }
        }

        private UpsellSuggestion _currentSuggestion;
        public UpsellSuggestion CurrentSuggestion
        {
            get => _currentSuggestion;
            set { _currentSuggestion = value; OnPropertyChanged(nameof(CurrentSuggestion)); }
        }

        public async Task CheckForUpsellAsync()
        {
            if (CurrentOrder == null || CurrentOrder.BranchId <= 0) return;

            var selectedIds = Services?.Where(s => s.IsSelected).Select(s => s.Id).ToList();
            if (selectedIds == null || !selectedIds.Any())
            {
                CurrentSuggestion = null;
                return;
            }

            try
            {
                string query = string.Join("&", selectedIds.Select(id => $"currentServices={id}"));
                var response = await _apiService.GetFromJsonAsync<UpsellSuggestion>($"Upsell/suggest?{query}&branchId={CurrentOrder.BranchId}");
                CurrentSuggestion = response;
            }
            catch { CurrentSuggestion = null; }
        }

        public void ApplyUpsell()
        {
            if (CurrentSuggestion == null) return;

            var result = MessageBox.Show(
                $"Вы подтверждаете, что клиент изначально НЕ ПРОСИЛ эту услугу, и это ваша успешная допродажа?\n\n" +
                $"За обман системы бонус аннулируется.",
                "Подтверждение допродажи",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            var serviceVm = Services?.FirstOrDefault(s => s.Id == CurrentSuggestion.SuggestedServiceId);
            if (serviceVm != null)
            {
                serviceVm.IsSelected = true;

                string bonusNote = $"[АПСЕЛЛ: продана услуга '{serviceVm.Name}', бонус: +{CurrentSuggestion.BonusAmount:N0} ₽]";

                if (string.IsNullOrEmpty(CurrentOrder.Notes))
                    CurrentOrder.Notes = bonusNote;
                else if (!CurrentOrder.Notes.Contains("АПСЕЛЛ"))
                    CurrentOrder.Notes += $"\n{bonusNote}";

                Recalculate();
                CurrentSuggestion = null;
            }
        }

        // === СВОЙСТВА-ОБЕРТКИ (ТЕПЕРЬ ТОЛЬКО В ОДНОМ ЭКЗЕМПЛЯРЕ!) ===
        public decimal ServicesTotal => _currentCalc?.ServicesTotal ?? 0;
        public decimal FinalTotal => _currentCalc?.FinalPrice ?? 0;
        public decimal WasherEarningsDisplay => _currentCalc?.WasherEarnings ?? 0;
        public decimal CompanyEarningsDisplay => _currentCalc?.CompanyGrossEarnings ?? 0;
        public decimal FinalTotalWithExpenses => FinalTotal + TotalExpensesAmount;

        public decimal ExtraCost
        {
            get => CurrentOrder.ExtraCost;
            set
            {
                if (CurrentOrder.ExtraCost != value)
                {
                    CurrentOrder.ExtraCost = value;
                    OnPropertyChanged(nameof(ExtraCost));
                    Recalculate();
                }
            }
        }

        // === ЕДИНСТВЕННЫЙ МЕТОД РАСЧЕТА (АСИНХРОННЫЙ, ЧЕРЕЗ API) ===
        public async void Recalculate()
        {
            if (CurrentOrder == null || Services == null || _apiService == null) return;

            SyncServiceIds();

            var request = new OrderPreviewRequestDto
            {
                BranchId = CurrentOrder.BranchId > 0 ? CurrentOrder.BranchId : AppSettings.CurrentBranchId,
                WasherId = CurrentOrder.GetWasherId() ?? 0,
                ServiceIds = Services.Where(s => s.IsSelected).Select(s => s.Id).ToList(),
                BodyTypeCategory = CurrentOrder.BodyTypeCategory > 0 ? CurrentOrder.BodyTypeCategory : 1,
                ExtraCost = CurrentOrder.ExtraCost,
                DiscountPercent = CurrentOrder.DiscountPercent,
                DiscountAmount = CurrentOrder.DiscountAmount,
                Notes = CurrentOrder.Notes ?? ""
            };

            var calcResult = await _apiService.CalculateOrderPreviewAsync(request);
            _currentCalc = calcResult;

            OnPropertyChanged(nameof(ServicesTotal));
            OnPropertyChanged(nameof(FinalTotal));
            OnPropertyChanged(nameof(FinalTotalWithExpenses));
            OnPropertyChanged(nameof(WasherEarningsDisplay));
            OnPropertyChanged(nameof(CompanyEarningsDisplay));

            _ = CheckForUpsellAsync();
        }

        private void FilterServicesByDepartment()
        {
            if (_allServicesCache == null) return;

            var selectedIds = CurrentOrder?.ServiceIds?.ToList() ?? new List<int>();

            var filtered = _currentDepartment == "Service"
                ? _allServicesCache.Where(s => s.ServiceCategory == AccuratSystem.Contracts.Enums.ServiceCategory.Service).ToList()
                : _allServicesCache.Where(s => s.ServiceCategory == AccuratSystem.Contracts.Enums.ServiceCategory.Wash).ToList();

            Services = new ObservableCollection<ServiceViewModel>(
                filtered.Select(s => new ServiceViewModel
                {
                    Id = s.Id,
                    Name = s.Name,
                    Price = s.PriceByBodyType.TryGetValue(SelectedBodyTypeCategory, out var p) ? p :
                           (s.PriceByBodyType.TryGetValue(1, out var def) ? def : 0),
                    IsSelected = selectedIds.Contains(s.Id)
                }));

            Recalculate();
        }

        public AddEditOrderViewModel()
        {
            _apiService = new ApiService();
        }

        public void Initialize(ContractsShift currentShift, ContractsOrder order = null)
        {
            _currentShift = currentShift;
            _existingOrder = order;

            _isEditMode = order != null && order.Id > 0;
            _isAppointment = order != null && order.IsAppointment;
            _currentCalc = null;

            InitializeOrder();
            _ = LoadWashersAsync();
            _ = LoadServicesAsync();
        }

        public void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private ContractsOrder _currentOrder;
        private ObservableCollection<ServiceViewModel> _services;
        private List<ContractsUser> _washers;
        private int _selectedBodyTypeCategory = 1;
        private string _windowTitle;
        private decimal _discountPercent;
        private decimal _discountAmount;

        public ContractsOrder CurrentOrder
        {
            get => _currentOrder;
            set { _currentOrder = value; _currentCalc = null; OnPropertyChanged(nameof(CurrentOrder)); }
        }

        public ObservableCollection<ServiceViewModel> Services
        {
            get => _services;
            set { _services = value; OnPropertyChanged(nameof(Services)); }
        }

        public List<ContractsUser> Washers
        {
            get => _washers;
            set { _washers = value; OnPropertyChanged(nameof(Washers)); }
        }

        public int SelectedBodyTypeCategory
        {
            get => _selectedBodyTypeCategory;
            set
            {
                if (_selectedBodyTypeCategory != value)
                {
                    _selectedBodyTypeCategory = value;

                    if (CurrentOrder != null)
                    {
                        CurrentOrder.BodyTypeCategory = value;
                    }

                    OnPropertyChanged(nameof(SelectedBodyTypeCategory));
                    UpdateServicePrices();
                }
            }
        }

        public string WindowTitle => _windowTitle;
        public bool IsEditMode => _isEditMode;
        public bool IsAppointment => _isAppointment;

        public decimal DiscountPercent
        {
            get => _discountPercent;
            set
            {
                if (_discountPercent != value)
                {
                    _discountPercent = value;
                    CurrentOrder.DiscountPercent = value;
                    if (value > 0) { _discountAmount = 0; CurrentOrder.DiscountAmount = 0; }
                    OnPropertyChanged(nameof(DiscountPercent));
                    Recalculate();
                }
            }
        }

        public decimal DiscountAmount
        {
            get => _discountAmount;
            set
            {
                if (_discountAmount != value)
                {
                    _discountAmount = value;
                    CurrentOrder.DiscountAmount = value;
                    if (value > 0) { _discountPercent = 0; CurrentOrder.DiscountPercent = 0; }
                    OnPropertyChanged(nameof(DiscountAmount));
                    Recalculate();
                }
            }
        }

        public void SyncServiceIds()
        {
            if (CurrentOrder != null && Services != null)
                CurrentOrder.ServiceIds = Services.Where(s => s.IsSelected).Select(s => s.Id).ToList();
        }

        private void OnDataChanged()
        {
            _ = LoadServicesAsync();
            Recalculate();
        }

        public async Task<(bool success, string message)> SaveOrderAsync()
        {
            if (!Validate()) return (false, "Ошибка валидации");

            SyncServiceIds();
            CurrentOrder.TotalPrice = ServicesTotal;
            CurrentOrder.OriginalTotalPrice = ServicesTotal;
            CurrentOrder.BodyTypeCategory = SelectedBodyTypeCategory;
            CurrentOrder.FinalPrice = FinalTotal;

            if (CurrentOrder.GetWasherId().HasValue)
            {
                CurrentOrder.OrderWashers = new List<AccuratSystem.Contracts.Models.OrderWasher>
                {
                    new AccuratSystem.Contracts.Models.OrderWasher
                    {
                        UserId = CurrentOrder.GetWasherId().Value,
                        SplitShare = 1.0m
                    }
                };
            }

            try
            {
                if (IsEditMode)
                {
                    await _apiService.UpdateOrderAsync(CurrentOrder);
                    return (true, "Заказ обновлен!");
                }
                else
                {
                    CurrentOrder.ShiftId = _currentShift?.Id ?? 0;
                    await _apiService.CreateOrderAsync(CurrentOrder);
                    return (true, "Заказ создан!");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка API: {ex.Message}");
            }
        }

        private void InitializeOrder()
        {
            if (_existingOrder != null && _isEditMode)
            {
                CurrentOrder = new ContractsOrder
                {
                    Id = _existingOrder.Id,
                    CarModel = _existingOrder.CarModel,
                    CarNumber = _existingOrder.CarNumber,
                    CarBodyType = _existingOrder.CarBodyType,
                    BodyTypeCategory = _existingOrder.BodyTypeCategory,
                    Time = _existingOrder.Time,
                    BoxNumber = _existingOrder.BoxNumber,
                    ServiceIds = new List<int>(_existingOrder.ServiceIds),
                    ExtraCost = _existingOrder.ExtraCost,
                    ExtraCostReason = _existingOrder.ExtraCostReason,
                    Status = _existingOrder.Status,
                    PaymentMethod = _existingOrder.PaymentMethod,
                    IsAppointment = _existingOrder.IsAppointment,
                    ShiftId = _existingOrder.ShiftId,
                    ClientId = _existingOrder.ClientId,
                    Notes = _existingOrder.Notes,
                    DiscountPercent = _existingOrder.DiscountPercent,
                    DiscountAmount = _existingOrder.DiscountAmount,
                    OriginalTotalPrice = _existingOrder.OriginalTotalPrice,
                    BranchId = _existingOrder.BranchId,
                    Department = _existingOrder.Department,
                    AdminId = _existingOrder.AdminId,
                    DurationMinutes = _existingOrder.DurationMinutes,
                    OrderWashers = _existingOrder.OrderWashers != null
                        ? _existingOrder.OrderWashers.ToList()
                        : new List<AccuratSystem.Contracts.Models.OrderWasher>()
                };

                _discountPercent = CurrentOrder.DiscountPercent;
                _discountAmount = CurrentOrder.DiscountAmount;
                SelectedBodyTypeCategory = CurrentOrder.BodyTypeCategory > 0 ? CurrentOrder.BodyTypeCategory : 1;
                _currentDepartment = CurrentOrder.Department ?? "Wash";
                OnPropertyChanged(nameof(CurrentDepartment));

                _windowTitle = _isAppointment ? "✏ Редактирование записи" : "✏ Редактирование заказа";
            }
            else if (_existingOrder != null && _isAppointment)
            {
                CurrentOrder = new ContractsOrder
                {
                    Id = 0,
                    CarModel = _existingOrder.CarModel,
                    CarNumber = _existingOrder.CarNumber,
                    CarBodyType = _existingOrder.CarBodyType,
                    BodyTypeCategory = _existingOrder.BodyTypeCategory,
                    Time = _existingOrder.Time,
                    BoxNumber = _existingOrder.BoxNumber,
                    ServiceIds = new List<int>(_existingOrder.ServiceIds),
                    ExtraCost = _existingOrder.ExtraCost,
                    ExtraCostReason = _existingOrder.ExtraCostReason,
                    Status = "Предварительная запись",
                    PaymentMethod = "Не указано",
                    IsAppointment = true,
                    ShiftId = 0,
                    BranchId = _existingOrder.BranchId,
                    ClientId = _existingOrder.ClientId,
                    Notes = _existingOrder.Notes,
                    DiscountPercent = _existingOrder.DiscountPercent,
                    DiscountAmount = _existingOrder.DiscountAmount,
                    OriginalTotalPrice = _existingOrder.OriginalTotalPrice,
                    Department = _existingOrder.Department,
                    OrderWashers = _existingOrder.OrderWashers != null
                        ? _existingOrder.OrderWashers.ToList()
                        : new List<AccuratSystem.Contracts.Models.OrderWasher>()
                };

                _currentDepartment = CurrentOrder.Department ?? "Wash";
                OnPropertyChanged(nameof(CurrentDepartment));

                _windowTitle = "📅 Редактирование записи";
            }
            else
            {
                CurrentOrder = new ContractsOrder
                {
                    Id = 0,
                    CarModel = "",
                    CarNumber = "",
                    CarBodyType = "Седан",
                    BodyTypeCategory = 1,
                    Time = DateTime.Now,
                    BoxNumber = 1,
                    ServiceIds = new List<int>(),
                    ExtraCost = 0,
                    ExtraCostReason = "",
                    Status = "В работе",
                    PaymentMethod = "Наличные",
                    IsAppointment = false,
                    ClientId = null,
                    Notes = "",
                    BranchId = AppSettings.CurrentBranchId,
                    AdminId = App.CurrentUser?.Id,
                    OrderWashers = new List<AccuratSystem.Contracts.Models.OrderWasher>()
                };

                _currentDepartment = CurrentOrder.Department ?? "Wash";
                OnPropertyChanged(nameof(CurrentDepartment));

                _windowTitle = "➕ Добавление заказа";
            }

            OnPropertyChanged(nameof(CurrentOrder));
            OnPropertyChanged(nameof(WindowTitle));
        }

        public void SetAsOrder()
        {
            _isAppointment = false;
            OnPropertyChanged(nameof(IsAppointment));
            _windowTitle = "✏ Редактирование заказа";
            OnPropertyChanged(nameof(WindowTitle));
        }

        public async Task LoadWashersAsync()
        {
            Washers = await _apiService.GetUsersAsync();
            OnPropertyChanged(nameof(Washers));
        }

        public async Task LoadServicesAsync()
        {
            _allServicesCache = await _apiService.GetServicesAsync();
            var selectedIds = CurrentOrder?.ServiceIds?.ToList() ?? new List<int>();

            if (Services == null)
            {
                Services = new ObservableCollection<ServiceViewModel>(
                    _allServicesCache.Select(s => new ServiceViewModel
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Price = s.PriceByBodyType.TryGetValue(SelectedBodyTypeCategory, out var p) ? p :
                               (s.PriceByBodyType.TryGetValue(1, out var def) ? def : 0),
                        IsSelected = selectedIds.Contains(s.Id)
                    }));
            }
            else
            {
                foreach (var vm in Services.ToList())
                {
                    var updated = _allServicesCache.FirstOrDefault(s => s.Id == vm.Id);
                    if (updated != null)
                    {
                        vm.Price = updated.PriceByBodyType.TryGetValue(SelectedBodyTypeCategory, out var p) ? p :
                                  (updated.PriceByBodyType.TryGetValue(1, out var def) ? def : 0);
                        vm.Name = updated.Name;
                        vm.IsSelected = selectedIds.Contains(vm.Id);
                    }
                }
                foreach (var s in _allServicesCache)
                {
                    if (!Services.Any(x => x.Id == s.Id))
                        Services.Add(new ServiceViewModel
                        {
                            Id = s.Id,
                            Name = s.Name,
                            Price = s.PriceByBodyType.TryGetValue(SelectedBodyTypeCategory, out var p) ? p :
                                   (s.PriceByBodyType.TryGetValue(1, out var def) ? def : 0),
                            IsSelected = selectedIds.Contains(s.Id)
                        });
                }
                var existingIds = new HashSet<int>(_allServicesCache.Select(s => s.Id));
                foreach (var s in Services.Where(s => !existingIds.Contains(s.Id)).ToList()) Services.Remove(s);
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                Recalculate();
                FilterServices();
            });
        }

        public void UpdateServicePrices()
        {
            if (Services != null && _allServicesCache.Any())
            {
                var availableServices = _currentDepartment == "Service"
                    ? _allServicesCache.Where(s => s.ServiceCategory == AccuratSystem.Contracts.Enums.ServiceCategory.Service).ToList()
                    : _allServicesCache.Where(s => s.ServiceCategory == AccuratSystem.Contracts.Enums.ServiceCategory.Wash).ToList();

                var selectedIds = Services.Where(s => s.IsSelected).Select(s => s.Id).ToList();
                var newServices = new ObservableCollection<ServiceViewModel>();

                foreach (var s in _allServicesCache)
                {
                    newServices.Add(new ServiceViewModel
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Price = s.PriceByBodyType.TryGetValue(SelectedBodyTypeCategory, out var p) ? p :
                               (s.PriceByBodyType.TryGetValue(1, out var def) ? def : 0),
                        IsSelected = selectedIds.Contains(s.Id)
                    });
                }

                Services = newServices;
                Recalculate();
                FilterServices();
            }
        }

        public bool Validate()
        {
            if (!IsAppointment)
            {
                if (!CurrentOrder.GetWasherId().HasValue || CurrentOrder.GetWasherId().Value <= 0)
                {
                    MessageBox.Show("Выберите сотрудника (мойщика), который выполнял заказ!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (CurrentOrder.ShiftId <= 0)
                {
                    MessageBox.Show("Нет активной смены для привязки заказа!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(CurrentOrder.CarModel))
            {
                MessageBox.Show("Введите марку и модель автомобиля", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(CurrentOrder.CarNumber))
            {
                MessageBox.Show("Введите государственный номер", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (Services?.Any(s => s.IsSelected) != true)
            {
                MessageBox.Show("Выберите хотя бы одну услугу", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (ExtraCost > 0 && string.IsNullOrWhiteSpace(CurrentOrder.ExtraCostReason))
            {
                MessageBox.Show("Укажите причину дополнительной стоимости", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (CurrentOrder.Status == "Выполнен")
            {
                if (string.IsNullOrWhiteSpace(CurrentOrder.PaymentMethod) || CurrentOrder.PaymentMethod == "Не указано")
                {
                    MessageBox.Show("Для завершения заказа необходимо выбрать способ оплаты!", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        public void Cleanup()
        {
            _isSubscribedToDataChanged = false;
        }

        private ContractsClient _selectedClient;
        public ContractsClient SelectedClient
        {
            get => _selectedClient;
            set
            {
                if (_selectedClient != value)
                {
                    _selectedClient = value;
                    OnPropertyChanged(nameof(SelectedClient));

                    if (value != null && CurrentOrder != null)
                    {
                        CurrentOrder.ClientId = value.Id;

                        if (value.DefaultDiscountPercent > 0 && DiscountPercent == 0)
                        {
                            DiscountPercent = value.DefaultDiscountPercent;
                        }

                        CarModel = value.CarModel;
                        CarNumber = value.CarNumber;
                    }
                }
            }
        }

        public string CarModel
        {
            get => CurrentOrder?.CarModel;
            set
            {
                if (CurrentOrder != null)
                {
                    CurrentOrder.CarModel = value;
                    OnPropertyChanged(nameof(CarModel));
                    OnPropertyChanged(nameof(CurrentOrder));
                }
            }
        }

        public string CarNumber
        {
            get => CurrentOrder?.CarNumber;
            set
            {
                if (CurrentOrder != null)
                {
                    CurrentOrder.CarNumber = value;
                    OnPropertyChanged(nameof(CarNumber));
                    OnPropertyChanged(nameof(CurrentOrder));
                }
            }
        }

        public int? SelectedWasherId
        {
            get => CurrentOrder?.GetWasherId();
            set
            {
                if (CurrentOrder != null && CurrentOrder.GetWasherId() != value)
                {
                    CurrentOrder.SetWasherId(value);
                    OnPropertyChanged(nameof(SelectedWasherId));
                    Recalculate();
                }
            }
        }

        private string _serviceSearchText = "";
        public string ServiceSearchText
        {
            get => _serviceSearchText;
            set
            {
                if (_serviceSearchText != value)
                {
                    _serviceSearchText = value;
                    OnPropertyChanged(nameof(ServiceSearchText));
                    FilterServices();
                }
            }
        }

        private void FilterServices()
        {
            if (_allServicesCache == null) return;

            var selectedIds = CurrentOrder?.ServiceIds?.ToList() ?? new List<int>();

            var filtered = _currentDepartment == "Service"
                ? _allServicesCache.Where(s => s.ServiceCategory == AccuratSystem.Contracts.Enums.ServiceCategory.Service)
                : _allServicesCache.Where(s => s.ServiceCategory == AccuratSystem.Contracts.Enums.ServiceCategory.Wash);

            if (!string.IsNullOrWhiteSpace(_serviceSearchText))
            {
                string search = _serviceSearchText.ToLower();
                filtered = filtered.Where(s =>
                    s.Name.ToLower().Contains(search) ||
                    (s.Description != null && s.Description.ToLower().Contains(search)));
            }

            Services = new ObservableCollection<ServiceViewModel>(
                filtered.Select(s => new ServiceViewModel
                {
                    Id = s.Id,
                    Name = s.Name,
                    Price = s.PriceByBodyType.TryGetValue(SelectedBodyTypeCategory, out var p) ? p :
                           (s.PriceByBodyType.TryGetValue(1, out var def) ? def : 0),
                    IsSelected = selectedIds.Contains(s.Id)
                }));

            Recalculate();
        }

        private ObservableCollection<ZoneItem> _availableZones = new ObservableCollection<ZoneItem>();
        public ObservableCollection<ZoneItem> AvailableZones
        {
            get => _availableZones;
            set { _availableZones = value; OnPropertyChanged(nameof(AvailableZones)); }
        }

        public void UpdateAvailableZones(List<ZoneItem> allZones)
        {
            var filtered = allZones.Where(z => z.Department == _currentDepartment).ToList();
            AvailableZones = new ObservableCollection<ZoneItem>(filtered);

            if (CurrentOrder?.BoxNumber > 0 && !filtered.Any(z => z.BoxNumber == CurrentOrder.BoxNumber))
            {
                CurrentOrder.BoxNumber = filtered.FirstOrDefault()?.BoxNumber ?? 0;
                CurrentOrder.Department = _currentDepartment;
            }

            OnPropertyChanged(nameof(AvailableZones));
        }

        private ObservableCollection<OrderExpense> _orderExpenses = new ObservableCollection<OrderExpense>();
        public ObservableCollection<OrderExpense> OrderExpenses
        {
            get => _orderExpenses;
            set { _orderExpenses = value; OnPropertyChanged(nameof(OrderExpenses)); }
        }

        private ObservableCollection<TimelineEntryViewModel> _orderTimeline = new ObservableCollection<TimelineEntryViewModel>();
        public ObservableCollection<TimelineEntryViewModel> OrderTimeline
        {
            get => _orderTimeline;
            set { _orderTimeline = value; OnPropertyChanged(nameof(OrderTimeline)); }
        }

        public decimal TotalExpensesAmount => OrderExpenses?.Sum(e => e.ClientPrice * e.Quantity) ?? 0;

        public async Task LoadOrderExpensesAsync(int orderId)
        {
            try
            {
                var expenses = await _apiService.GetOrderExpensesAsync(orderId);
                OrderExpenses = new ObservableCollection<OrderExpense>(expenses);
                OnPropertyChanged(nameof(TotalExpensesAmount));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки расходов: {ex.Message}");
            }
        }

        public async Task LoadOrderTimelineAsync(int orderId)
        {
            try
            {
                var entries = await _apiService.GetOrderTimelineAsync(orderId);
                OrderTimeline = new ObservableCollection<TimelineEntryViewModel>(
                    entries.Select(e => new TimelineEntryViewModel(e)));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки ленты: {ex.Message}");
            }
        }

        public async Task<bool> AddExpenseAsync(string name, string category, decimal costPrice, decimal clientPrice, int quantity, string note)
        {
            if (CurrentOrder?.Id <= 0) return false;

            var dto = new AddOrderExpenseDto
            {
                OrderId = CurrentOrder.Id,
                Name = name,
                Category = (ExpenseCategory)Enum.Parse(typeof(ExpenseCategory), category),
                CostPrice = costPrice,
                ClientPrice = clientPrice,
                Quantity = quantity,
                Note = note,
                CreatedByUser = App.CurrentUser?.DisplayString
            };

            try
            {
                var newExpense = await _apiService.AddOrderExpenseAsync(CurrentOrder.Id, dto);
                OrderExpenses.Add(newExpense);
                OnPropertyChanged(nameof(TotalExpensesAmount));
                await LoadOrderTimelineAsync(CurrentOrder.Id);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка добавления расхода: {ex.Message}");
                return false;
            }
        }
    }
}