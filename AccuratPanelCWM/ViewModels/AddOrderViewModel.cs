using AccuratPanelCWM.Services;
using AccuratSystem.Contracts.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AccuratPanelCWM.ViewModels
{
    // Обертка для услуги, чтобы цена динамически менялась на экране
    public partial class ServiceUiWrapper : ObservableObject
    {
        public int Id { get; set; }
        public string Name { get; set; }
        [ObservableProperty] private decimal _displayPrice;
    }

    public partial class AddOrderViewModel : ObservableObject
    {
        private readonly ApiService _apiService;

        private int _branchId;
        private int _boxNumber;
        private string _department;
        private List<Service> _rawServices = new();
        private int? _detectedClientId = null;
        private decimal _clientDiscountPercent = 0;

        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _boxInfoText;

        [ObservableProperty] private string _carNumber;
        [ObservableProperty] private string _carModel;

        [ObservableProperty] private int _selectedBodyTypeIndex = 0;

        [ObservableProperty] private List<User> _washers = new();
        [ObservableProperty] private User _selectedWasher;

        [ObservableProperty] private ObservableCollection<ServiceUiWrapper> _availableServices = new();
        [ObservableProperty] private IList<object> _selectedServices = new List<object>();

        [ObservableProperty] private string _totalPriceDisplay = "0 ₽";

        public AddOrderViewModel(ApiService apiService)
        {
            _apiService = apiService;
        }

        // Метод для инициализации (вместо конструктора страницы)
        public void Initialize(int branchId, int boxNumber, string department, string boxName)
        {
            _branchId = branchId;
            _boxNumber = boxNumber;
            _department = department;
            BoxInfoText = $"Создание заказа: {boxName}";

            LoadDataCommand.Execute(null);
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            IsBusy = true;
            try
            {
                var allUsers = await _apiService.GetUsersAsync();
                Washers = allUsers.Where(u => u.IsActive).ToList();

                _rawServices = await _apiService.GetServicesAsync();

                AvailableServices.Clear();
                foreach (var service in _rawServices)
                {
                    AvailableServices.Add(new ServiceUiWrapper
                    {
                        Id = service.Id,
                        Name = service.Name,
                        DisplayPrice = service.PriceByBodyType.TryGetValue(1, out var p) ? p : 0
                    });
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", ex.Message, "ОК");
            }
            finally { IsBusy = false; }
        }

        // Авто-срабатывание при смене типа кузова
        partial void OnSelectedBodyTypeIndexChanged(int value)
        {
            int category = value + 1; // 0 -> Категория 1
            foreach (var svc in AvailableServices)
            {
                var raw = _rawServices.FirstOrDefault(r => r.Id == svc.Id);
                if (raw != null)
                {
                    svc.DisplayPrice = raw.PriceByBodyType.TryGetValue(category, out var price) ? price : 0;
                }
            }
            CalculateTotalCommand.Execute(null); // Пересчитываем ИТОГО
        }

        // Авто-срабатывание при вводе госномера
        partial void OnCarNumberChanged(string value)
        {
            if (value?.Length >= 3)
            {
                SearchClientCommand.Execute(value.ToUpper());
            }
        }

        [RelayCommand]
        private async Task SearchClientAsync(string number)
        {
            var client = await _apiService.GetClientByNumberAsync(number);
            if (client != null)
            {
                _detectedClientId = client.Id;
                _clientDiscountPercent = client.DefaultDiscountPercent;
                CarModel = client.CarModel;

                if (number.Length >= 6) CarNumber = client.CarNumber;

                CalculateTotalCommand.Execute(null);
            }
        }

        [RelayCommand]
        private void CalculateTotal()
        {
            if (SelectedServices == null) return;

            decimal total = SelectedServices.Cast<ServiceUiWrapper>().Sum(s => s.DisplayPrice);

            if (_clientDiscountPercent > 0)
            {
                total -= total * (_clientDiscountPercent / 100m);
                TotalPriceDisplay = $"{total:N0} ₽ (скидка {_clientDiscountPercent}%)";
            }
            else
            {
                TotalPriceDisplay = $"{total:N0} ₽";
            }
        }

        [RelayCommand]
        private async Task SaveOrderAsync()
        {
            if (string.IsNullOrWhiteSpace(CarNumber) || SelectedWasher == null || !SelectedServices.Any())
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", "Заполните номер, исполнителя и услуги", "ОК");
                return;
            }

            IsBusy = true;
            try
            {
                var shifts = await _apiService.GetShiftsAsync();
                var activeShift = shifts.FirstOrDefault(s => !s.IsClosed && s.BranchId == _branchId);

                if (activeShift == null)
                {
                    await Application.Current.MainPage.DisplayAlert("Внимание", "Нет открытой смены!", "ОК");
                    return;
                }

                decimal total = SelectedServices.Cast<ServiceUiWrapper>().Sum(s => s.DisplayPrice);

                var newOrder = new Order
                {
                    CarNumber = CarNumber.Trim().ToUpper(),
                    ClientId = _detectedClientId,
                    CarModel = CarModel?.Trim() ?? "Не указана",
                    BodyTypeCategory = SelectedBodyTypeIndex + 1,
                    OrderWashers = new List<OrderWasher>
                    {
                        new OrderWasher { UserId = SelectedWasher.Id, SplitShare = 1.0m }
                    },
                    ServiceIds = SelectedServices.Cast<ServiceUiWrapper>().Select(s => s.Id).ToList(),
                    TotalPrice = total,
                    OriginalTotalPrice = total,
                    FinalPrice = total,
                    Status = "В работе",
                    PaymentMethod = "Наличные",
                    Time = DateTime.UtcNow,
                    BranchId = _branchId,
                    BoxNumber = _boxNumber,
                    Department = _department,
                    ShiftId = activeShift.Id
                };

                await _apiService.CreateOrderAsync(newOrder);
                await Application.Current.MainPage.Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", ex.Message, "ОК");
            }
            finally { IsBusy = false; }
        }
    }
}