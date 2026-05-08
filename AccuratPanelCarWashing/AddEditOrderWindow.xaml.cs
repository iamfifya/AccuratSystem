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
                // 1. Загружаем мойщиков, клиентов И ФИЛИАЛЫ с сервера
                var allUsers = await _apiService.GetUsersAsync();
                var allClients = await _apiService.GetClientsAsync();
                var allBranches = await _apiService.GetBranchesAsync();

                WasherComboBox.ItemsSource = allUsers;
                _viewModel.Washers = allUsers;
                ClientComboBox.ItemsSource = allClients;

                //  ГЕНЕРИРУЕМ ДИНАМИЧЕСКИЕ ЗОНЫ ДЛЯ ТЕКУЩЕГО ФИЛИАЛА
                int branchId = _currentShift?.BranchId ?? AppSettings.CurrentBranchId;
                var currentBranch = allBranches.FirstOrDefault(b => b.Id == branchId);
                var availableZones = new List<ZoneItem>();

                if (currentBranch != null)
                {
                    for (int i = 1; i <= currentBranch.WashBaysCount; i++)
                        availableZones.Add(new ZoneItem { Name = $"🧽 Бокс {i} (Мойка)", BoxNumber = i, Department = "Wash" });

                    for (int i = 1; i <= currentBranch.ServiceLiftsCount; i++)
                        availableZones.Add(new ZoneItem { Name = $"🔧 Подъемник {i} (Сервис)", BoxNumber = i, Department = "Service" });
                }

                ZoneComboBox.ItemsSource = availableZones;

                // Если это редактирование — выставляем текущие значения
                if (order != null)
                {
                    if (order.IsAppointment)
                    {
                        DurationTextBox.Text = order.DurationMinutes > 0 ? order.DurationMinutes.ToString() : "60";
                    }

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

                    //  Выбираем нужную зону из списка
                    var selectedZone = availableZones.FirstOrDefault(z => z.BoxNumber == order.BoxNumber && z.Department == order.Department);
                    if (selectedZone != null)
                        ZoneComboBox.SelectedItem = selectedZone;

                    OrderDatePicker.SelectedDate = order.Time;
                    OrderTimeTextBox.Text = order.Time.ToString("HH:mm");
                }
                else
                {
                    // Для нового заказа
                    OrderDatePicker.SelectedDate = DateTime.Now;
                    OrderTimeTextBox.Text = DateTime.Now.ToString("HH:mm");
                    if (allUsers.Any()) WasherComboBox.SelectedItem = allUsers.FirstOrDefault();

                    // Выбираем первый свободный бокс по умолчанию
                    if (availableZones.Any()) ZoneComboBox.SelectedItem = availableZones.First();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных с сервера: {ex.Message}", "Ошибка API");
            }
        }



        private void SetupStaticLists()
        {
            BodyTypeComboBox.ItemsSource = new List<KeyValuePair<string, int>> {
        new KeyValuePair<string, int>("Категория 1 (Легковая)", 1),
        new KeyValuePair<string, int>("Категория 2 (Универсал)", 2),
        new KeyValuePair<string, int>("Категория 3 (Кроссовер)", 3),
        new KeyValuePair<string, int>("Категория 4 (Внедорожник)", 4)
    };
            BodyTypeComboBox.DisplayMemberPath = "Key";
            BodyTypeComboBox.SelectedValuePath = "Value";

            StatusComboBox.ItemsSource = new List<KeyValuePair<string, string>> {
        new KeyValuePair<string, string>("🟢 В работе", "В работе"),
        new KeyValuePair<string, string>("✅ Выполнен", "Выполнен"),
        new KeyValuePair<string, string>("❌ Отменен", "Отменен")
    };
            StatusComboBox.DisplayMemberPath = "Key";
            StatusComboBox.SelectedValuePath = "Value";

            PaymentMethodComboBox.ItemsSource = new List<KeyValuePair<string, string>> {
        new KeyValuePair<string, string>("❓ Не указано", "Не указано"),
        new KeyValuePair<string, string>("💵 Наличные", "Наличные"),
        new KeyValuePair<string, string>("💳 Карта", "Карта"),
        new KeyValuePair<string, string>("📱 Перевод", "Перевод"),
        new KeyValuePair<string, string>("📱 QR-код", "QR-код")
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
                        _viewModel.CurrentOrder.Time = DateTime.SpecifyKind(localTime, DateTimeKind.Utc);
                    }
                    else
                    {
                        MessageBox.Show("Неверный формат времени (ЧЧ:ММ)"); return;
                    }
                }

                // 2. Собираем данные из комбобоксов
                if (WasherComboBox.SelectedValue is int wid) _viewModel.CurrentOrder.WasherId = wid;
                if (ClientComboBox.SelectedValue is int cid) _viewModel.CurrentOrder.ClientId = cid;
                if (StatusComboBox.SelectedValue is string st) _viewModel.CurrentOrder.Status = st;
                if (PaymentMethodComboBox.SelectedValue is string pm) _viewModel.CurrentOrder.PaymentMethod = pm;

                //  3. ЧИТАЕМ ДАННЫЕ ИЗ ВЫБРАННОЙ ЗОНЫ
                if (ZoneComboBox.SelectedItem is ZoneItem selectedZone)
                {
                    _viewModel.CurrentOrder.BoxNumber = selectedZone.BoxNumber;
                    _viewModel.CurrentOrder.Department = selectedZone.Department; // Автоматически "Wash" или "Service"!
                }

                // 🔥 ПЕРЕНЕСЛИ ВЫШЕ: Подстраховка: если BranchId почему-то остался нулем, берем текущий филиал
                if (_viewModel.CurrentOrder.BranchId <= 0)
                {
                    _viewModel.CurrentOrder.BranchId = AppSettings.CurrentBranchId;
                }

                // ПРОВЕРКА ДОСТУПНОСТИ ДЛЯ ЗАПИСЕЙ 
                if (_viewModel.CurrentOrder.IsAppointment)
                {
                    if (int.TryParse(DurationTextBox.Text, out int duration))
                        _viewModel.CurrentOrder.DurationMinutes = duration;
                    else
                        _viewModel.CurrentOrder.DurationMinutes = 60; // По умолчанию

                    // 🔥 ИСПРАВЛЕНО: Теперь передаем BranchId первым параметром!
                    bool isAvailable = await _apiService.CheckBoxAvailabilityForAppointmentAsync(
                        _viewModel.CurrentOrder.BranchId,       // 1. Филиал
                        _viewModel.CurrentOrder.BoxNumber,      // 2. Номер бокса
                        _viewModel.CurrentOrder.Time,           // 3. Время начала
                        _viewModel.CurrentOrder.DurationMinutes,// 4. Длительность
                        _viewModel.CurrentOrder.Id);            // 5. Игнорируем сам заказ при редактировании

                    if (!isAvailable)
                    {
                        MessageBox.Show($"Время {_viewModel.CurrentOrder.Time:HH:mm} в выбранной зоне уже занято!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return; // Останавливаем сохранение
                    }
                }

                this.IsEnabled = false; // Блокируем UI на время запроса

                if (_currentShift != null)
                {
                    _viewModel.CurrentOrder.ShiftId = _currentShift.Id;
                    _viewModel.CurrentOrder.BranchId = _currentShift.BranchId;
                }

                // 4. Вызываем асинхронное сохранение во ViewModel
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
                _viewModel.CurrentOrder.Status = "В работе";
                SaveButton_Click(sender, e);
            }
        }
    }
    public class ZoneItem
    {
        public string Name { get; set; }
        public int BoxNumber { get; set; }
        public string Department { get; set; }
    }
}
