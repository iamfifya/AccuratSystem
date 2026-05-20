// === ЯВНЫЕ АЛИАСЫ ТОЛЬКО ДЛЯ КОНФЛИКТНЫХ ИМЁН ===
// UI-модель пользователя (с IsAdmin, DisplayString) — используем для _currentUser
using AccuratPanelCarWashing.Models;
// Остальные using
using AccuratPanelCarWashing.Services; // Для методов расширения (GetWasherId/SetWasherId)
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ContractsClient = AccuratSystem.Contracts.Models.Client;
// Контрактные модели из API — используем для всех данных с сервера
using ContractsOrder = AccuratSystem.Contracts.Models.Order;
using ContractsService = AccuratSystem.Contracts.Models.Service;
using ContractsShift = AccuratSystem.Contracts.Models.Shift;
using ContractsUser = AccuratSystem.Contracts.Models.User;
using WpfUser = AccuratPanelCarWashing.Models.User;

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
        private List<ContractsService> _allServicesCache = new List<ContractsService>(); // Контрактный тип

        public AddEditOrderViewModel()
        {
            _apiService = new ApiService();
        }

        public void Initialize(ContractsShift currentShift, ContractsOrder order = null)
        {
            _currentShift = currentShift;
            _existingOrder = order;
            _isEditMode = order != null && order.Id > 0;
            _isAppointment = order != null && order.IsAppointment && order.Id == 0;
            _currentCalc = null;

            InitializeOrder();
            _ = LoadWashersAsync();
            _ = LoadServicesAsync();
        }

        public void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private ContractsOrder _currentOrder;
        private ObservableCollection<ServiceViewModel> _services;
        private List<ContractsUser> _washers; // Контрактный тип для данных из API
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

        // === ОБЁРТКИ НАД OrderMath (чтобы XAML не ломался) ===
        public decimal ServicesTotal => CurrentCalculation.ServicesTotal;
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
        public decimal FinalTotal => CurrentCalculation.FinalPrice;
        public decimal WasherEarningsDisplay => CurrentCalculation.WasherEarnings;
        public decimal CompanyEarningsDisplay => CurrentCalculation.CompanyEarnings;

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

        // === ГЛАВНЫЙ МЕТОД РАСЧЁТА ===
        private OrderCalculation CurrentCalculation
        {
            get
            {
                if (_currentCalc == null)
                {
                    // Передаем список контрактных типов
                    _currentCalc = OrderMath.Calculate(CurrentOrder, _allServicesCache, _washers);
                }
                return _currentCalc;
            }
        }

        public void Recalculate()
        {
            _currentCalc = null;

            // СИНХРОНИЗИРУЕМ ВЫБРАННЫЕ УСЛУГИ С МОДЕЛЬЮ ПЕРЕД РАСЧЕТОМ
            SyncServiceIds();

            // ОТЛАДКА:
            var selectedCount = Services?.Count(s => s.IsSelected) ?? 0;
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Recalculate: {selectedCount} услуг выбрано");
            if (Services != null)
            {
                foreach (var s in Services.Where(s => s.IsSelected))
                {
                    System.Diagnostics.Debug.WriteLine($"  - {s.Name}: {s.Price} ₽");
                }
            }

            System.Diagnostics.Debug.WriteLine($"  ServicesTotal: {ServicesTotal}");
            System.Diagnostics.Debug.WriteLine($"  FinalTotal: {FinalTotal}");
            System.Diagnostics.Debug.WriteLine($"  WasherEarnings: {WasherEarningsDisplay}");

            OnPropertyChanged(nameof(FinalTotal));
            OnPropertyChanged(nameof(WasherEarningsDisplay));
            OnPropertyChanged(nameof(CompanyEarningsDisplay));
            OnPropertyChanged(nameof(ServicesTotal));
        }


        // === Просто синхронизирует выбранные услуги с заказом ===
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
        // Сохранение теперь асинхронное и возвращает результат
        public async Task<(bool success, string message)> SaveOrderAsync()
        {
            if (!Validate()) return (false, "Ошибка валидации");

            SyncServiceIds();
            CurrentOrder.TotalPrice = ServicesTotal;
            CurrentOrder.OriginalTotalPrice = ServicesTotal;
            CurrentOrder.BodyTypeCategory = SelectedBodyTypeCategory;
            CurrentOrder.FinalPrice = FinalTotal; // Обязательно считаем итог для БД

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
                    Department = _existingOrder.Department
                };
                _discountPercent = CurrentOrder.DiscountPercent;
                _discountAmount = CurrentOrder.DiscountAmount;
                SelectedBodyTypeCategory = CurrentOrder.BodyTypeCategory;
                _windowTitle = "✏ Редактирование заказа";
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
                    IsAppointment = true,
                    ClientId = _existingOrder.ClientId,
                    Notes = _existingOrder.Notes,
                    DiscountPercent = _existingOrder.DiscountPercent,
                    DiscountAmount = _existingOrder.DiscountAmount,
                    OriginalTotalPrice = _existingOrder.OriginalTotalPrice,
                    BranchId = _existingOrder.BranchId,
                    Department = _existingOrder.Department
                };
                SelectedBodyTypeCategory = CurrentOrder.BodyTypeCategory;
                _windowTitle = "✏ Редактирование записи";
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
                    BranchId = AppSettings.CurrentBranchId
                };
                _windowTitle = "➕ Добавление заказа";
            }
        }

        public async Task LoadWashersAsync()
        {
            Washers = await _apiService.GetUsersAsync(); // Возвращает List<ContractsUser>
            OnPropertyChanged(nameof(Washers));
        }

        public async Task LoadServicesAsync()
        {
            // Грузим с сервера контрактные сервисы
            _allServicesCache = await _apiService.GetServicesAsync();
            var selectedIds = CurrentOrder?.ServiceIds?.ToList() ?? new List<int>();

            if (Services == null)
            {
                Services = new ObservableCollection<ServiceViewModel>(
                    _allServicesCache.Select(s => new ServiceViewModel
                    {
                        Id = s.Id,
                        Name = s.Name,
                        // Вычисляем цену вручную через PriceByBodyType
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
                // Обновляем коллекцию в потоке UI
                Recalculate();
            });
        }

        public void UpdateServicePrices()
        {
            if (Services != null && _allServicesCache.Any())
            {
                // Запоминаем, какие услуги уже были отмечены галочками
                var selectedIds = Services.Where(s => s.IsSelected).Select(s => s.Id).ToList();

                // Создаем новую коллекцию, чтобы WPF гарантированно перерисовал интерфейс
                var newServices = new ObservableCollection<ServiceViewModel>();

                foreach (var s in _allServicesCache)
                {
                    newServices.Add(new ServiceViewModel
                    {
                        Id = s.Id,
                        Name = s.Name,
                        // Вычисляем цену вручную
                        Price = s.PriceByBodyType.TryGetValue(SelectedBodyTypeCategory, out var p) ? p :
                               (s.PriceByBodyType.TryGetValue(1, out var def) ? def : 0),
                        IsSelected = selectedIds.Contains(s.Id)
                    });
                }

                Services = newServices; // Обновляем свойство, UI перерисовывает список с новыми ценами
                Recalculate();
            }
        }

        public bool Validate()
        {
            if (!IsAppointment)  // Только для обычных заказов
            {
                // Проверяем, выбран ли исполнитель через метод расширения
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
            // Если подписки нет — метод можно оставить пустым
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

                        // Присваиваем значения через свойства-обертки. 
                        CarModel = value.CarModel;
                        CarNumber = value.CarNumber;
                    }
                }
            }
        }

        // === ОБЁРТКИ ДЛЯ НАДЁЖНОЙ ПРИВЯЗКИ ===
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
            get => CurrentOrder?.GetWasherId(); // Используем метод расширения
            set
            {
                if (CurrentOrder != null && CurrentOrder.GetWasherId() != value)
                {
                    CurrentOrder.SetWasherId(value); // Используем метод расширения
                    OnPropertyChanged(nameof(SelectedWasherId));
                    // Пересчитываем деньги на экране сразу при выборе другого мойщика!
                    Recalculate();
                }
            }
        }
    }
}