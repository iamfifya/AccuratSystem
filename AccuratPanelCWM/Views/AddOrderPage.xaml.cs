using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using AccuratPanelCWM.Models;
using AccuratPanelCWM.Services;

namespace AccuratPanelCWM.Views
{
    public partial class AddOrderPage : ContentPage
    {
        private readonly ApiService _apiService;
        private readonly int _branchId;
        private readonly int _boxNumber;
        private readonly string _department;

        private List<Service> _allServices = new List<Service>();

        // Обертка для UI, чтобы обновлять цену при смене кузова
        public class ServiceUiWrapper
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public decimal DisplayPrice { get; set; }
        }

        private int? _detectedClientId = null;
        private decimal _clientDiscountPercent = 0;

        public AddOrderPage(int branchId, int boxNumber, string department, string boxName)
        {
            InitializeComponent();
            _apiService = new ApiService();
            _branchId = branchId;
            _boxNumber = boxNumber;
            _department = department;

            BoxInfoLabel.Text = $"Создание заказа: {boxName}";
            BodyTypePicker.SelectedIndex = 0; // По умолчанию 1 категория

            _ = LoadDataAsync();
        }

        private async void OnCarNumberChanged(object sender, TextChangedEventArgs e)
        {
            string number = e.NewTextValue?.Trim().ToUpper();

            // Снижаем порог до 3-х символов для быстрого поиска
            if (!string.IsNullOrEmpty(number) && number.Length >= 3)
            {
                // Вызываем поиск (он должен уметь искать подстроку)
                var client = await _apiService.GetClientByNumberAsync(number);

                if (client != null)
                {
                    _detectedClientId = client.Id;
                    _clientDiscountPercent = client.DefaultDiscountPercent;
                    CarModelEntry.Text = client.CarModel;

                    // Если это точное совпадение — можно и номер "добить" в поле ввода
                    if (number.Length >= 6)
                    {
                        CarNumberEntry.Text = client.CarNumber;
                    }

                    CalculateTotal();
                }
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                // 1. Загружаем ВООБЩЕ ВСЕХ пользователей
                var allUsers = await _apiService.GetUsersAsync();

                // 2. Оставляем только активных (Всех, кто может мыть)
                var activeStaff = allUsers.Where(u => u.IsActive).ToList();
                WasherPicker.ItemsSource = activeStaff;

                // 3. Загружаем услуги
                _allServices = await _apiService.GetServicesAsync();
                UpdateServicesList();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка", $"Не удалось загрузить данные: {ex.Message}", "ОК");
            }
        }

        // Обновляем список услуг при смене кузова
        private void BodyTypePicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateServicesList();
            CalculateTotal();
        }

        private void UpdateServicesList()
        {
            if (_allServices == null || !_allServices.Any()) return;

            int bodyCategory = BodyTypePicker.SelectedIndex + 1; // Индекс 0 = 1 категория

            //  Добавили защиту от NULL (?. и ??)
            var selectedIds = ServicesCollectionView.SelectedItems?
                                .Cast<ServiceUiWrapper>()
                                .Select(s => s.Id).ToList() ?? new List<int>();

            var uiServices = new List<ServiceUiWrapper>();
            foreach (var service in _allServices)
            {
                uiServices.Add(new ServiceUiWrapper
                {
                    Id = service.Id,
                    Name = service.Name,
                    DisplayPrice = service.GetPrice(bodyCategory)
                });
            }

            ServicesCollectionView.ItemsSource = uiServices;

            // Восстанавливаем выделение
            var itemsToSelect = uiServices.Where(s => selectedIds.Contains(s.Id)).ToList();
            ServicesCollectionView.SelectedItems = new List<object>(itemsToSelect);
        }

        // Пересчет итоговой цены
        private void ServicesCollectionView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CalculateTotal();
        }

        private void CalculateTotal()
        {
            decimal total = 0;
            if (ServicesCollectionView.SelectedItems != null)
            {
                foreach (ServiceUiWrapper selectedService in ServicesCollectionView.SelectedItems)
                {
                    total += selectedService.DisplayPrice;
                }
            }

            // Применяем скидку, если она есть
            if (_clientDiscountPercent > 0)
            {
                decimal discountAmount = total * (_clientDiscountPercent / 100m);
                total -= discountAmount;
            }

            TotalPriceLabel.Text = _clientDiscountPercent > 0
                ? $"{total:N0} ₽ (со скидкой {_clientDiscountPercent}%)"
                : $"{total:N0} ₽";
        }

        // Сохранение заказа
        private async void SaveOrderButton_Clicked(object sender, EventArgs e)
        {
            // 1. Валидация
            if (string.IsNullOrWhiteSpace(CarNumberEntry.Text))
            {
                await DisplayAlert("Ошибка", "Введите госномер", "ОК");
                return;
            }
            if (WasherPicker.SelectedItem == null)
            {
                await DisplayAlert("Ошибка", "Выберите исполнителя (мойщика)", "ОК");
                return;
            }

            if (ServicesCollectionView.SelectedItems == null || ServicesCollectionView.SelectedItems.Count == 0)
            {
                await DisplayAlert("Ошибка", "Выберите хотя бы одну услугу", "ОК");
                return;
            }

            // Блокируем кнопку от двойного нажатия
            var btn = (Button)sender;
            btn.IsEnabled = false;

            try
            {
                // --- ИЩЕМ АКТИВНУЮ СМЕНУ ---
                var shifts = await _apiService.GetShiftsAsync();
                var activeShift = shifts.FirstOrDefault(s => !s.IsClosed && s.BranchId == _branchId);

                if (activeShift == null)
                {
                    await DisplayAlert("Внимание", "В этом филиале нет открытой смены! Сначала откройте смену.", "ОК");
                    btn.IsEnabled = true;
                    return;
                }

                // 2. Формируем заказ
                var selectedWasher = (User)WasherPicker.SelectedItem;
                decimal total = ServicesCollectionView.SelectedItems.Cast<ServiceUiWrapper>().Sum(s => s.DisplayPrice);
                var selectedServiceIds = ServicesCollectionView.SelectedItems.Cast<ServiceUiWrapper>().Select(s => s.Id).ToList();

                var newOrder = new CarWashOrder
                {
                    CarNumber = CarNumberEntry.Text.Trim().ToUpper(),
                    ClientId = _detectedClientId,
                    CarModel = CarModelEntry.Text?.Trim() ?? "Не указана",
                    BodyTypeCategory = BodyTypePicker.SelectedIndex + 1,
                    CarBodyType = BodyTypePicker.SelectedItem.ToString(),
                    WasherId = selectedWasher.Id,
                    ServiceIds = selectedServiceIds,
                    TotalPrice = total,
                    OriginalTotalPrice = total,
                    FinalPrice = total,
                    Status = "В работе",
                    PaymentMethod = "Наличные",
                    Time = DateTime.UtcNow,
                    BranchId = _branchId,
                    BoxNumber = _boxNumber,
                    Department = _department,

                    // ПРИСВАИВАЕМ ID СМЕНЫ (Без этого WPF его не покажет!)
                    ShiftId = activeShift.Id
                };

                // 3. Отправка на сервер
                await _apiService.CreateOrderAsync(newOrder);

                await DisplayAlert("Успех", "Заказ успешно создан!", "ОК");

                // Возвращаемся на главную
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка сохранения", ex.Message, "ОК");
                btn.IsEnabled = true;
            }
        }
    }
}