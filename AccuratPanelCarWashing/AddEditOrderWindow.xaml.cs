using DocumentFormat.OpenXml.Bibliography;
using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using AccuratPanelCarWashing.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AccuratPanelCarWashing
{
    public partial class AddEditOrderWindow : Window
    {
        private readonly ApiService _apiService;              // Для работы с сервером
        private readonly Shift _currentShift;
        private readonly AddEditOrderViewModel _viewModel;

        public AddEditOrderWindow(
            AddEditOrderViewModel viewModel,
            Shift currentShift = null,
            CarWashOrder order = null)
        {
            InitializeComponent();

            _apiService = new ApiService();
            _currentShift = currentShift;
            _viewModel = viewModel;

            // 1. Инициализируем ViewModel (она внутри себя загрузит услуги через API)
            _viewModel.Initialize(currentShift, order);
            DataContext = _viewModel;

            // 2. Загружаем справочники (мойщики, клиенты) асинхронно
            _ = LoadDictionariesAsync(order);

            // 3. Заполняем статические списки в UI (категории, оплаты)
            SetupStaticLists();
        }

        private async Task LoadDictionariesAsync(CarWashOrder order)
        {
            try
            {
                // Загружаем мойщиков и клиентов с сервера
                var allUsers = await _apiService.GetUsersAsync();
                var allClients = await _apiService.GetClientsAsync();

                // Обновляем комбобоксы
                WasherComboBox.ItemsSource = allUsers;
                _viewModel.Washers = allUsers;
                ClientComboBox.ItemsSource = allClients;

                // Если это редактирование — выставляем текущие значения
                if (order != null)
                {
                    if (order.WasherId > 0)
                        WasherComboBox.SelectedValue = order.WasherId;

                    if (order.ClientId.HasValue)
                        ClientComboBox.SelectedValue = order.ClientId.Value;

                    if (order.BodyTypeCategory > 0)
                        BodyTypeComboBox.SelectedValue = order.BodyTypeCategory.ToString();

                    if (!string.IsNullOrEmpty(order.Status))
                        StatusComboBox.SelectedValue = order.Status;

                    if (!string.IsNullOrEmpty(order.PaymentMethod))
                        PaymentMethodComboBox.SelectedValue = order.PaymentMethod;

                    // Время
                    OrderDatePicker.SelectedDate = order.Time;
                    OrderTimeTextBox.Text = order.Time.ToString("HH:mm");
                }
                else
                {
                    // Для нового заказа
                    OrderDatePicker.SelectedDate = DateTime.Now;
                    OrderTimeTextBox.Text = DateTime.Now.ToString("HH:mm");
                    if (allUsers.Any()) WasherComboBox.SelectedItem = allUsers.FirstOrDefault();
                }

                

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных с сервера: {ex.Message}", "Ошибка API");
            }
        }

        private void SetupStaticLists()
        {
            BodyTypeComboBox.ItemsSource = new List<KeyValuePair<string, string>> {
        new KeyValuePair<string, string>("Категория 1 (Легковая)", "1"),
        new KeyValuePair<string, string>("Категория 2 (Универсал)", "2"),
        new KeyValuePair<string, string>("Категория 3 (Кроссовер)", "3"),
        new KeyValuePair<string, string>("Категория 4 (Внедорожник)", "4")
    };
            BodyTypeComboBox.DisplayMemberPath = "Key";
            BodyTypeComboBox.SelectedValuePath = "Value";

            StatusComboBox.ItemsSource = new List<KeyValuePair<string, string>> {
        new KeyValuePair<string, string>("🟢 Выполняется", "Выполняется"),
        new KeyValuePair<string, string>("✅ Выполнен", "Выполнен"),
        new KeyValuePair<string, string>("❌ Отменен", "Отменен")
    };
            StatusComboBox.DisplayMemberPath = "Key";
            StatusComboBox.SelectedValuePath = "Value";

            PaymentMethodComboBox.ItemsSource = new List<KeyValuePair<string, string>> {
        new KeyValuePair<string, string>("💵 Наличные", "Наличные"),
        new KeyValuePair<string, string>("💳 Карта", "Карта"),
        new KeyValuePair<string, string>("📱 Перевод", "Перевод")
    };
            PaymentMethodComboBox.DisplayMemberPath = "Key";
            PaymentMethodComboBox.SelectedValuePath = "Value";
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Собираем время
                if (OrderDatePicker.SelectedDate.HasValue)
                {
                    if (TimeSpan.TryParse(OrderTimeTextBox.Text, out var time))
                    {
                        DateTime localTime = OrderDatePicker.SelectedDate.Value.Date + time;
                        // ИСПРАВЛЕНИЕ: Конвертируем в UTC
                        _viewModel.CurrentOrder.Time = DateTime.SpecifyKind(localTime, DateTimeKind.Utc);
                    }
                    else
                    {
                        MessageBox.Show("Неверный формат времени (ЧЧ:ММ)"); return;
                    }
                }

                // 2. Собираем данные из комбобоксов, которые не привязаны напрямую
                if (WasherComboBox.SelectedValue is int wid) _viewModel.CurrentOrder.WasherId = wid;
                if (ClientComboBox.SelectedValue is int cid) _viewModel.CurrentOrder.ClientId = cid;
                if (StatusComboBox.SelectedValue is string st) _viewModel.CurrentOrder.Status = st;
                if (PaymentMethodComboBox.SelectedValue is string pm) _viewModel.CurrentOrder.PaymentMethod = pm;

                this.IsEnabled = false; // Блокируем UI на время запроса

                // 3. Вызываем асинхронное сохранение во ViewModel (которое стучит в API)
                var result = await _viewModel.SaveOrderAsync();

                if (result.success)
                {
                    MessageBox.Show(result.message, "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show(result.message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критическая ошибка: {ex.Message}");
            }
            finally
            {
                this.IsEnabled = true;
            }
        }

        private void ServicesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Как только пользователь кликает по услуге (выбирает или снимает выбор),
            // мы заставляем ViewModel пересчитать всю математику.
            if (DataContext is AddEditOrderViewModel vm)
            {
                vm.Recalculate();
            }
        }

        private void AddNewClient_Click(object sender, RoutedEventArgs e)
        {
            var win = new AddEditClientWindow(null);
            if (win.ShowDialog() == true)
            {
                // Не забываем передать CurrentOrder, как это было в твоем коде
                _ = LoadDictionariesAsync(_viewModel.CurrentOrder);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        // Метод для "Преобразования записи в заказ"
        private async void ConvertToOrderButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Преобразовать запись в активный заказ?", "Подтверждение", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                _viewModel.CurrentOrder.IsAppointment = false;
                _viewModel.CurrentOrder.Status = "Выполняется";
                SaveButton_Click(sender, e);
            }
        }
    }
}
