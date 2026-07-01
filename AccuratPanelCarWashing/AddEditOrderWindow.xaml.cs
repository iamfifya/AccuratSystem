using DocumentFormat.OpenXml.Bibliography;

// === ЯВНЫЕ АЛИАСЫ ДЛЯ КОНТРАКТНЫХ МОДЕЛЕЙ ===
using ContractsOrder = AccuratSystem.Contracts.Models.Order;
using ContractsService = AccuratSystem.Contracts.Models.Service;
using ContractsUser = AccuratSystem.Contracts.Models.User;
using ContractsBranch = AccuratSystem.Contracts.Models.Branch;
using ContractsShift = AccuratSystem.Contracts.Models.Shift;
using ContractsClient = AccuratSystem.Contracts.Models.Client;

// Остальные using
using AccuratPanelCarWashing.Services; // <-- ВАЖНО: для методов расширения (GetWasherId/SetWasherId)
using AccuratPanelCarWashing.ViewModels;
using AccuratPanelCarWashing.Models;   // <-- ВАЖНО: для AppSettings
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
        private readonly ContractsShift _currentShift;
        private readonly AddEditOrderViewModel _viewModel;

        public AddEditOrderWindow(
            AddEditOrderViewModel viewModel,
            ContractsShift currentShift = null,
            ContractsOrder order = null)
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

            // Инициализируем выбранный департамент
            if (!string.IsNullOrEmpty(_viewModel.CurrentOrder?.Department))
            {
                // Устанавливаем через свойство ViewModel, чтобы сработала фильтрация
                _viewModel.CurrentDepartment = _viewModel.CurrentOrder.Department;
                DepartmentComboBox.SelectedValue = _viewModel.CurrentDepartment;
            }
            else
            {
                // По умолчанию — Мойка (это вызовет FilterServicesByDepartment())
                DepartmentComboBox.SelectedValue = "Wash";
                _viewModel.CurrentDepartment = "Wash";
            }

            // Подписка на смену департамента и филиала для обновления зон
            DepartmentComboBox.SelectionChanged += DepartmentComboBox_SelectionChanged;
            BranchComboBox.SelectionChanged += BranchComboBox_SelectionChanged; // Добавили подписку здесь!
        }

        // Обработчик: при смене департамента обновляем список зон
        private void DepartmentComboBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded && DataContext is AddEditOrderViewModel vm)
            {
                // Обновляем департамент в заказе
                vm.CurrentOrder.Department = DepartmentComboBox.SelectedValue?.ToString() ?? "Wash";

                // Пересчитываем доступные зоны для нового департамента
                // Для этого нужно заново сгенерировать список всех зон филиала
                if (BranchComboBox.SelectedValue is int branchId)
                {
                    var currentBranch = _allBranches?.FirstOrDefault(b => b.Id == branchId);
                    if (currentBranch != null)
                    {
                        var allZones = new List<ZoneItem>();
                        for (int i = 1; i <= currentBranch.WashBaysCount; i++)
                            allZones.Add(new ZoneItem { Name = $"🧽 Бокс {i} (Мойка)", BoxNumber = i, Department = "Wash" });
                        for (int i = 1; i <= currentBranch.ServiceLiftsCount; i++)
                            allZones.Add(new ZoneItem { Name = $"🔧 Подъемник {i} (Сервис)", BoxNumber = i, Department = "Service" });

                        vm.UpdateAvailableZones(allZones);
                    }
                }
            }
        }

        private List<ContractsBranch> _allBranches; // Добавляем кэш филиалов на уровне класса

        private async Task LoadDictionariesAsync(ContractsOrder order)
        {
            try
            {
                var allUsers = await _apiService.GetUsersAsync();
                var allClients = await _apiService.GetClientsAsync();
                _allBranches = await _apiService.GetBranchesAsync(); // Загружаем филиалы

                // Загружаем категории кузовов для текущего филиала
                // 1. Если мы редактируем существующий заказ, берем его филиал
                // 2. Если заказ новый, но есть активная смена — берем филиал смены (самый надежный вариант)
                // 3. В крайнем случае — берем из настроек
                int currentBranchId = 0;

                if (order != null && order.BranchId > 0)
                    currentBranchId = order.BranchId;
                else if (_currentShift != null)
                    currentBranchId = _currentShift.BranchId;
                else
                    currentBranchId = AppSettings.CurrentBranchId;

                // Добавь лог, чтобы в будущем видеть это в консоли (если включишь Debug)
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Loading dictionaries for BranchId: {currentBranchId}");

                var carCategories = await _apiService.GetCarCategoriesAsync(currentBranchId);

                // Привязываем к UI
                BodyTypeComboBox.ItemsSource = carCategories;
                BodyTypeComboBox.DisplayMemberPath = "Name";
                BodyTypeComboBox.SelectedValuePath = "Id";

                // ЗАГРУЗКА СПОСОБОВ ОПЛАТЫ
                var paymentMethods = await _apiService.GetPaymentMethodsAsync(currentBranchId);
                PaymentMethodComboBox.ItemsSource = paymentMethods;
                PaymentMethodComboBox.DisplayMemberPath = "Name"; // Что видит пользователь
                PaymentMethodComboBox.SelectedValuePath = "Name";

                // СТАТУСЫ ЗАКАЗОВ
                var orderStatuses = await _apiService.GetOrderStatusesAsync(currentBranchId);
                StatusComboBox.ItemsSource = orderStatuses;
                StatusComboBox.DisplayMemberPath = "DisplayName"; // Покажет "🟢 В работе"
                StatusComboBox.SelectedValuePath = "Name";

                WasherComboBox.ItemsSource = allUsers;
                _viewModel.Washers = allUsers;
                ClientComboBox.ItemsSource = allClients;
                BranchComboBox.ItemsSource = _allBranches; // Привязываем список филиалов

                // Загружаем расходы и ленту, если заказ уже создан (не новый)
                if (order != null && order.Id > 0)
                {
                    _ = _viewModel.LoadOrderExpensesAsync(order.Id);
                    _ = _viewModel.LoadOrderTimelineAsync(order.Id);
                }

                if (order != null)
                {
                    if (order.IsAppointment)
                        DurationTextBox.Text = order.DurationMinutes > 0 ? order.DurationMinutes.ToString() : "60";

                    // ИСПРАВЛЕНО: Используем метод расширения GetWasherId()
                    if (order.GetWasherId() > 0) WasherComboBox.SelectedValue = order.GetWasherId();
                    if (order.ClientId.HasValue) ClientComboBox.SelectedValue = order.ClientId.Value;
                    if (order.BodyTypeCategory > 0) BodyTypeComboBox.SelectedValue = order.BodyTypeCategory.ToString();
                    if (!string.IsNullOrEmpty(order.Status)) StatusComboBox.SelectedValue = order.Status;
                    if (!string.IsNullOrEmpty(order.PaymentMethod)) PaymentMethodComboBox.SelectedValue = order.PaymentMethod;

                    OrderDatePicker.SelectedDate = order.Time;
                    OrderTimeTextBox.Text = order.Time.ToString("HH:mm");

                    // Устанавливаем текущий филиал и генерируем зоны
                    BranchComboBox.SelectedValue = order.BranchId > 0 ? order.BranchId : (_currentShift?.BranchId ?? AppSettings.CurrentBranchId);
                    UpdateZones(order);
                }
                else
                {
                    OrderDatePicker.SelectedDate = DateTime.Now;
                    OrderTimeTextBox.Text = DateTime.Now.ToString("HH:mm");
                    if (allUsers.Any()) WasherComboBox.SelectedItem = allUsers.FirstOrDefault();

                    //  Берем филиал строго из модели (которую мы пробросили с главной вкладки!)
                    BranchComboBox.SelectedValue = _viewModel.CurrentOrder.BranchId;
                    UpdateZones(null);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных с сервера: {ex.Message}", "Ошибка API");
            }
        }

        // Событие: если администратор переключил филиал ручками
        private void BranchComboBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            // Защита от вызова метода до того, как окно полностью загрузилось
            if (this.IsLoaded)
            {
                UpdateZones(null);
            }
        }

        // Метод генерации боксов для выбранного филиала
        private void UpdateZones(ContractsOrder order)
        {
            if (BranchComboBox.SelectedValue is int branchId)
            {
                var currentBranch = _allBranches?.FirstOrDefault(b => b.Id == branchId);
                var allZones = new List<ZoneItem>();

                if (currentBranch != null)
                {
                    // Генерируем ВСЕ зоны филиала из БД
                    for (int i = 1; i <= currentBranch.WashBaysCount; i++)
                        allZones.Add(new ZoneItem { Name = $"🧽 Бокс {i} (Мойка)", BoxNumber = i, Department = "Wash" });

                    for (int i = 1; i <= currentBranch.ServiceLiftsCount; i++)
                        allZones.Add(new ZoneItem { Name = $"🔧 Подъемник {i} (Сервис)", BoxNumber = i, Department = "Service" });
                }

                // ПЕРЕДАЕМ все зоны в ViewModel для фильтрации
                _viewModel.UpdateAvailableZones(allZones);

                // Пытаемся выбрать зону
                if (order != null && _viewModel.AvailableZones.Any(z => z.BoxNumber == order.BoxNumber && z.Department == order.Department))
                {
                    ZoneComboBox.SelectedValue = order.BoxNumber;
                }
                else if (_viewModel.AvailableZones.Any())
                {
                    // Если заказ новый или старая зона не подходит - выбираем ПЕРВУЮ доступную
                    ZoneComboBox.SelectedValue = _viewModel.AvailableZones.First().BoxNumber;
                    _viewModel.CurrentOrder.BoxNumber = _viewModel.AvailableZones.First().BoxNumber;
                }
                else
                {
                    ZoneComboBox.SelectedValue = null;
                }
            }
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
                if (WasherComboBox.SelectedValue is int wid)
                {
                    // ИСПРАВЛЕНО: Используем метод расширения SetWasherId()
                    _viewModel.CurrentOrder.SetWasherId(wid);
                }
                if (ClientComboBox.SelectedValue is int cid) _viewModel.CurrentOrder.ClientId = cid;
                if (StatusComboBox.SelectedValue is string st && !string.IsNullOrWhiteSpace(st))
                {
                    _viewModel.CurrentOrder.Status = st;
                }
                else if (string.IsNullOrEmpty(_viewModel.CurrentOrder.Status))
                {
                    _viewModel.CurrentOrder.Status = "В работе"; // Дефолтный статус, если не установлен
                }
                if (PaymentMethodComboBox.SelectedValue is string pm) _viewModel.CurrentOrder.PaymentMethod = pm;

                //  3. ЧИТАЕМ ДАННЫЕ ИЗ ВЫБРАННОЙ ЗОНЫ
                if (ZoneComboBox.SelectedItem is ZoneItem selectedZone)
                {
                    _viewModel.CurrentOrder.BoxNumber = selectedZone.BoxNumber;
                    _viewModel.CurrentOrder.Department = selectedZone.Department; // Автоматически "Wash" или "Service"!
                }

                // ПЕРЕНЕСЛИ ВЫШЕ: Подстраховка: если BranchId почему-то остался нулем, берем текущий филиал
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

                    // ИСПРАВЛЕНО: Теперь передаем BranchId первым параметром!
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

                if (_viewModel.CurrentOrder.IsAppointment)
                {
                    _viewModel.CurrentOrder.ShiftId = 0;
                }
                else
                {
                    // Если это заказ, смена должна быть открыта
                    if (_currentShift != null && !_currentShift.IsClosed)
                    {
                        _viewModel.CurrentOrder.ShiftId = _currentShift.Id;
                        _viewModel.CurrentOrder.BranchId = _currentShift.BranchId;
                    }
                    else
                    {
                        MessageBox.Show("Нельзя сохранить заказ: на этом филиале нет активной смены!\nСначала начните смену.",
                            "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);

                        // Обязательно возвращаем UI в рабочее состояние перед выходом
                        this.IsEnabled = true;
                        return;
                    }
                }

                // ПРОВЕРКА: нельзя создать заказ сервиса для филиала без сервисных зон
                if (_viewModel.CurrentOrder.Department == "Service")
                {
                    var branch = _allBranches?.FirstOrDefault(b => b.Id == _viewModel.CurrentOrder.BranchId);
                    if (branch != null && branch.ServiceLiftsCount == 0)
                    {
                        MessageBox.Show(
                            $"В выбранном филиале \"{branch.Name}\" нет сервисных зон (подъемников).\n\n" +
                            $"Выберите филиал с сервисом или смените департамент на \"Мойка\".",
                            "Ошибка валидации",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
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
            if (_currentShift == null || _currentShift.IsClosed)
            {
                MessageBox.Show("Нельзя преобразовать запись в заказ: на этом филиале нет активной смены!\nСначала начните смену.",
                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show("Преобразовать запись в активный заказ?", "Подтверждение", MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;

            // Берём мойщиков прямо из кэша ViewModel, чтобы не делать лишний запрос к API
            var washers = _viewModel.Washers.Where(u => u.RoleId != 1 && u.RoleId != 2).ToList();

            if (!washers.Any())
            {
                MessageBox.Show("Нет доступных мойщиков!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var washerDialog = new WasherSelectionDialog(washers);
            if (washerDialog.ShowDialog() != true || washerDialog.SelectedWasher == null)
                return;

            var selectedWasher = washerDialog.SelectedWasher;

            try
            {
                this.IsEnabled = false;

                if (_viewModel.CurrentOrder.Id > 0)
                {
                    // Запись УЖЕ в базе. Делаем конвертацию строго через сервер
                    var convertedOrder = await _apiService.ConvertAppointmentToOrderAsync(
                        _viewModel.CurrentOrder.Id,
                        _currentShift.Id,
                        selectedWasher.Id);

                    MessageBox.Show($"✅ Запись преобразована в заказ!\nМойщик назначен: {selectedWasher.FullName}",
                        "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Закрываем окно. Заказ сохранен, UI на главной обновится по SignalR или вручную
                    DialogResult = true;
                    Close();
                }
                else
                {
                    // Это НОВАЯ запись, которой еще нет в базе (нажали Плюс -> Преобразовать).
                    // Просто меняем свойства вью-модели и вызываем обычное сохранение
                    _viewModel.SelectedWasherId = selectedWasher.Id;
                    _viewModel.CurrentOrder.IsAppointment = false;
                    _viewModel.CurrentOrder.Status = "В работе";
                    _viewModel.SetAsOrder();

                    SaveButton_Click(sender, e);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при преобразовании: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.IsEnabled = true;
            }
        }

        // Обработчик поиска по услугам
        private void ServiceSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is AddEditOrderViewModel vm)
            {
                vm.ServiceSearchText = ServiceSearchTextBox.Text;
            }
        }

        // === ОБРАБОТЧИК ДОБАВЛЕНИЯ РАСХОДА ===
        private async void AddExpenseButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ExpenseNameTextBox.Text))
            {
                MessageBox.Show("Введите название расхода", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(ExpenseCostPriceTextBox.Text.Replace(",", "."), out decimal costPrice) || costPrice < 0)
            {
                MessageBox.Show("Введите корректную себестоимость", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(ExpenseClientPriceTextBox.Text.Replace(",", "."), out decimal clientPrice) || clientPrice < 0)
            {
                MessageBox.Show("Введите корректную цену для клиента", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(ExpenseQuantityTextBox.Text, out int quantity) || quantity <= 0)
            {
                quantity = 1;
            }

            var categoryItem = ExpenseCategoryComboBox.SelectedItem as ComboBoxItem;
            if (categoryItem == null)
            {
                MessageBox.Show("Выберите категорию", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string category = categoryItem.Tag?.ToString() ?? "Consumables";

            string note = string.IsNullOrWhiteSpace(ExpenseNoteTextBox.Text) ? "" : ExpenseNoteTextBox.Text;

            this.IsEnabled = false;
            bool success = await _viewModel.AddExpenseAsync(
                ExpenseNameTextBox.Text,
                category,
                costPrice,
                clientPrice,
                quantity,
                note);
            this.IsEnabled = true;

            if (success)
            {
                MessageBox.Show("Расход добавлен!", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                // Очищаем поля
                ExpenseNameTextBox.Clear();
                ExpenseCostPriceTextBox.Clear();
                ExpenseClientPriceTextBox.Clear();
                ExpenseQuantityTextBox.Text = "1";
                ExpenseNoteTextBox.Clear();
                ExpenseCategoryComboBox.SelectedItem = -1;
            }
            else
            {
                MessageBox.Show("Не удалось добавить расход", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void PrintActButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.CurrentOrder?.Id <= 0)
            {
                MessageBox.Show("Сохраните заказ перед печатью акта", "Предупреждение");
                return;
            }

            // TODO: Здесь будет логика генерации акта
            // 1. Собрать данные: заказ, услуги, расходы, клиент
            // 2. Сформировать документ (ClosedXML для Excel или PDF)
            // 3. Открыть для печати

            MessageBox.Show("Функция печати акта в разработке!\n\nДанные для акта:\n" +
                           $"Заказ #{_viewModel.CurrentOrder.Id}\n" +
                           $"Клиент: {_viewModel.CurrentOrder.ClientId}\n" +
                           $"Услуги: {_viewModel.Services?.Count(s => s.IsSelected)} шт.\n" +
                           $"Расходы: {_viewModel.OrderExpenses?.Count} шт.\n" +
                           $"Итого: {_viewModel.FinalTotalWithExpenses:N0} ₽",
                           "Печать акта", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ApplyUpsell_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AddEditOrderViewModel vm)
            {
                vm.ApplyUpsell();
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