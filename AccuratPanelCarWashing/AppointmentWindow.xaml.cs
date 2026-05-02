using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using AccuratPanelCarWashing.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AccuratPanelCarWashing
{
    public partial class AppointmentWindow : Window
    {
        private AppointmentViewModel _viewModel;
        private int _selectedBox = 1;
        private readonly ApiService _apiService = new ApiService();

        // 1. УБРАЛИ SqliteDataService
        public AppointmentWindow()
        {
            InitializeComponent();

            _viewModel = App.GetService<AppointmentViewModel>();
            DataContext = _viewModel;

            var bodyTypes = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Категория 1 (Легковая)", "1"),
                new KeyValuePair<string, string>("Категория 2 (Универсал)", "2"),
                new KeyValuePair<string, string>("Категория 3 (Кроссовер)", "3"),
                new KeyValuePair<string, string>("Категория 4 (Внедорожник)", "4")
            };

            BodyTypeComboBox.ItemsSource = bodyTypes;
            BodyTypeComboBox.DisplayMemberPath = "Key";
            BodyTypeComboBox.SelectedValuePath = "Value";
            BodyTypeComboBox.SelectedValue = "1";

            AppointmentDatePicker.SelectedDate = DateTime.Now.AddDays(1);
            AppointmentTimeTextBox.Text = "12:00";
            DurationTextBox.Text = "60";
            ExtraCostTextBox.Text = "0";

            AppointmentTimeTextBox.TextChanged += (s, e) => CheckAvailability();
            DurationTextBox.TextChanged += (s, e) => CheckAvailability();
            Box1Radio.Checked += (s, e) => { _selectedBox = 1; CheckAvailability(); };
            Box2Radio.Checked += (s, e) => { _selectedBox = 2; CheckAvailability(); };
            Box3Radio.Checked += (s, e) => { _selectedBox = 3; CheckAvailability(); };

            ExtraCostTextBox.TextChanged += (s, e) =>
            {
                if (decimal.TryParse(ExtraCostTextBox.Text, out decimal cost))
                    _viewModel.ExtraCost = cost;
            };

            ServicesListBox.SelectionChanged += (s, e) =>
            {
                foreach (ServiceViewModel service in ServicesListBox.Items)
                {
                    service.IsSelected = ServicesListBox.SelectedItems.Contains(service);
                }
                _viewModel.CalculateTotal();
                CheckAvailability();
            };

            AppointmentDatePicker.SelectedDateChanged += (s, date) => CheckAvailability();

            BodyTypeComboBox.SelectionChanged += (s, e) =>
            {
                if (BodyTypeComboBox.SelectedItem is KeyValuePair<string, string> selectedItem)
                {
                    if (int.TryParse(selectedItem.Value, out int category))
                    {
                        _viewModel.SelectedBodyTypeCategory = category;
                    }
                }
            };

            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.ServicesTotal) ||
                    e.PropertyName == nameof(_viewModel.FinalTotal))
                {
                    UpdateTotalDisplay();
                }
            };

            UpdateTotalDisplay();
            CheckAvailability();
        }

        private void UpdateTotalDisplay()
        {
            ServicesTotalText.Text = $"💰 Услуги: {_viewModel.ServicesTotal:N0} ₽";
            ExtraCostText.Text = $"➕ Дополнительно: {_viewModel.ExtraCost:N0} ₽";
            TotalPriceTextBlock.Text = $"💰 Итого: {_viewModel.FinalTotal:N0} ₽";
        }

        private async void CheckAvailability()
        {
            try
            {
                var date = AppointmentDatePicker.SelectedDate;
                if (!date.HasValue || !DateTime.TryParse($"{date:yyyy-MM-dd} {AppointmentTimeTextBox.Text}", out DateTime startTime))
                {
                    AvailabilityText.Text = "❌ Некорректное время";
                    return;
                }

                if (!int.TryParse(DurationTextBox.Text, out int duration)) return;

                bool isAvailable = await _apiService.CheckBoxAvailabilityForAppointmentAsync(_selectedBox, startTime, duration);


                if (isAvailable)
                {
                    AvailabilityText.Text = "🟢 Время свободно";
                    AvailabilityText.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    AvailabilityText.Text = "🔴 Время уже занято!";
                    AvailabilityText.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
            catch { /* Игнорируем ошибки при вводе */ }
        }

        private void CheckAvailabilityButton_Click(object sender, RoutedEventArgs e) => CheckAvailability();

        private string GetCategoryName(int categoryId)
        {
            switch (categoryId)
            {
                case 1: return "Категория 1 (Легковая)";
                case 2: return "Категория 2 (Универсал)";
                case 3: return "Категория 3 (Кроссовер)";
                case 4: return "Категория 4 (Внедорожник)";
                default: return "Категория 1 (Легковая)";
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CarModelTextBox.Text)) { MessageBox.Show("Введите марку и модель автомобиля", "Ошибка"); return; }
                if (string.IsNullOrWhiteSpace(CarNumberTextBox.Text)) { MessageBox.Show("Введите государственный номер", "Ошибка"); return; }

                var date = AppointmentDatePicker.SelectedDate;
                if (!date.HasValue) { MessageBox.Show("Выберите дату записи", "Ошибка"); return; }

                if (!DateTime.TryParse($"{date:yyyy-MM-dd} {AppointmentTimeTextBox.Text}", out DateTime startTime))
                { MessageBox.Show("Введите корректное время (например 14:30)", "Ошибка"); return; }

                if (!int.TryParse(DurationTextBox.Text, out int duration) || duration < 15)
                { MessageBox.Show("Минимальная длительность - 15 минут", "Ошибка"); return; }

                if (startTime < date.Value.Date.AddHours(9)) { MessageBox.Show("Рабочий день начинается в 9:00", "Ошибка"); return; }
                if (startTime.AddMinutes(duration) > date.Value.Date.AddHours(19)) { MessageBox.Show("Рабочий день заканчивается в 19:00", "Ошибка"); return; }

                var selectedServices = _viewModel.GetSelectedServiceIds();
                if (!selectedServices.Any()) { MessageBox.Show("Выберите хотя бы одну услугу", "Ошибка"); return; }

                this.IsEnabled = false;

                bool isAvailable = await _apiService.CheckBoxAvailabilityForAppointmentAsync(_selectedBox, startTime, duration);
                if (!isAvailable)
                {
                    MessageBox.Show($"Время {startTime:HH:mm} уже занято другой записью на сервере!\n\nПожалуйста, выберите другое время.", "Ошибка");
                    this.IsEnabled = true;
                    return;
                }

                int bodyTypeCategory = _viewModel.SelectedBodyTypeCategory;
                string bodyTypeName = GetCategoryName(bodyTypeCategory);
                DateTime utcAppointmentDate = TimeHelper.ToUtc(startTime);

                var newOrder = new CarWashOrder
                {
                    Id = 0,
                    CarModel = CarModelTextBox.Text.Trim(),
                    CarNumber = CarNumberTextBox.Text.Trim().ToUpper(),
                    CarBodyType = bodyTypeName,
                    BodyTypeCategory = bodyTypeCategory,
                    Time = TimeHelper.ToUtc(startTime),  // или DateTime.SpecifyKind(startTime, DateTimeKind.Utc)
                    BoxNumber = _selectedBox,
                    ServiceIds = selectedServices,
                    ExtraCost = _viewModel.ExtraCost,
                    ExtraCostReason = ExtraCostReasonTextBox.Text.Trim(),

                    // 🔑 КЛЮЧЕВЫЕ ПОЛЯ ДЛЯ ЗАПИСИ:
                    IsAppointment = true,
                    Status = "Предварительная запись",  // Новый статус для записей
                    PaymentMethod = "Не указано",  // Для записей оплата ещё не выбрана
                    DurationMinutes = duration,  // Если есть такое поле в CarWashOrder

                    ClientId = null,  // Можно добавить выбор клиента
                    Notes = "",
                    DiscountPercent = 0,
                    DiscountAmount = 0,
                    OriginalTotalPrice = _viewModel.ServicesTotal,
                    TotalPrice = _viewModel.ServicesTotal,
                    FinalPrice = _viewModel.FinalTotal,
                    WasherId = 0,  // 0 = мойщик не назначен
                    ShiftId = 0,   // 0 = смена не назначена
                    BranchId = AppSettings.CurrentBranchId  // Если есть филиалы
                };

                await _apiService.CreateOrderAsync(newOrder);

                MessageBox.Show($"✅ Запись успешно создана!\n\n" +
                    $"🚗 {newOrder.CarModel} ({newOrder.CarNumber})\n" +
                    $"📅 {TimeHelper.ToMsk(newOrder.Time):dd.MM.yyyy HH:mm}\n" +
                    $"💰 Итого: {_viewModel.FinalTotal:N0} ₽",
                    "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сервера при сохранении: {ex.Message}", "Критическая ошибка");
                DialogResult = false;
            }
            finally
            {
                this.IsEnabled = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
