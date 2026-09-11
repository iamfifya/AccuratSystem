using AccuratPanelCWD.Services;
using AccuratPanelCWD.ViewModels;
using AccuratSystem.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ContractsUser = AccuratSystem.Contracts.Models.User;

namespace AccuratPanelCWD.Windows
{
    public partial class AuditLogWindow : Window
    {
        private readonly ApiService _apiService = new ApiService();

        public AuditLogWindow()
        {
            InitializeComponent();
            Loaded += AuditLogWindow_Loaded;
        }

        private async void AuditLogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadFiltersAsync();
            await LoadAuditLogAsync();
        }

        private async System.Threading.Tasks.Task LoadFiltersAsync()
        {
            try
            {
                // === Пользователи ===
                var users = await _apiService.GetUsersAsync();
                var userOptions = new List<KeyValuePair<string, int?>>
                {
                    new KeyValuePair<string, int?>("👥 Все пользователи", null)
                };
                foreach (var user in users)
                {
                    userOptions.Add(new KeyValuePair<string, int?>(
                        $"{user.FullName} ({GetRoleName(user.RoleId)})",
                        user.Id));
                }
                UserFilter.ItemsSource = userOptions;
                UserFilter.SelectedItem = userOptions.FirstOrDefault();

                // === Типы событий ===
                var eventTypes = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("📋 Все типы", "all"),
                    new KeyValuePair<string, string>("🏷 Скидка применена", "DiscountApplied"),
                    new KeyValuePair<string, string>("➕ Доплата изменена", "ExtraCostChanged"),
                    new KeyValuePair<string, string>("💰 Цена изменена", "PriceChanged"),
                    new KeyValuePair<string, string>("🅿️ Бокс изменён", "BoxChanged"),
                    new KeyValuePair<string, string>("👤 Мойщик изменён", "WasherChanged"),
                    new KeyValuePair<string, string>("💳 Способ оплаты", "PaymentMethodChanged"),
                    new KeyValuePair<string, string>("🕐 Время изменено", "TimeChanged"),
                    new KeyValuePair<string, string>("🔄 Статус изменён", "StatusChanged"),
                    new KeyValuePair<string, string>("📦 Заказ перенесён в другую смену", "ShiftTransferred")
                };
                EntryTypeFilter.ItemsSource = eventTypes;
                EntryTypeFilter.SelectedItem = eventTypes.FirstOrDefault();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки фильтров: {ex.Message}");
            }
        }

        private string GetRoleName(int roleId) => roleId switch
        {
            1 => "Директор",
            2 => "Администратор",
            3 => "Мойщик",
            4 => "Сотрудник сервиса",
            _ => "Сотрудник"
        };

        private async System.Threading.Tasks.Task LoadAuditLogAsync()
        {
            try
            {
                int? userId = null;
                var selectedUserValue = UserFilter.SelectedValue;
                if (selectedUserValue != null && selectedUserValue is int parsedId)
                {
                    userId = parsedId;
                }

                string entryType = null;
                var selectedTypeValue = EntryTypeFilter.SelectedValue;
                if (selectedTypeValue is string selectedType && selectedType != "all")
                {
                    entryType = selectedType;
                }

                DateTime? startDate = StartDateFilter.SelectedDate;
                DateTime? endDate = EndDateFilter.SelectedDate;

                var entries = await _apiService.GetAuditLogAsync(
                    userId: userId,
                    startDate: startDate,
                    endDate: endDate,
                    entryType: entryType,
                    pageSize: 500,
                    pageNumber: 1);

                AuditLogGrid.ItemsSource = entries;
                Title = $"📜 Журнал действий — {entries.Count} записей";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки журнала: {ex.Message}");
            }
        }

        private async void ApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            await LoadAuditLogAsync();
        }

        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            var userOptions = UserFilter.ItemsSource as List<KeyValuePair<string, int?>>;
            if (userOptions != null && userOptions.Any())
            {
                UserFilter.SelectedItem = userOptions.FirstOrDefault();
            }

            StartDateFilter.SelectedDate = null;
            EndDateFilter.SelectedDate = null;

            var eventTypes = EntryTypeFilter.ItemsSource as List<KeyValuePair<string, string>>;
            if (eventTypes != null && eventTypes.Any())
            {
                EntryTypeFilter.SelectedItem = eventTypes.FirstOrDefault();
            }

            LoadAuditLogAsync();
        }

        // === НОВОЕ: Обработчики двойного клика ===

        /// <summary>
        /// Открывает карточку сотрудника при двойном клике на имя пользователя
        /// </summary>
        private async void EmployeeName_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // ✅ ИСПРАВЛЕНО: проверяем, что это именно двойной клик
            if (e.ClickCount != 2) return;

            if (sender is System.Windows.Controls.TextBlock textBlock && textBlock.DataContext is OrderTimelineEntry entry)
            {
                if (entry.RelatedEntityId.HasValue)
                {
                    try
                    {
                        var users = await _apiService.GetUsersAsync();
                        var user = users.FirstOrDefault(u => u.Id == entry.RelatedEntityId.Value);

                        if (user != null)
                        {
                            var employeeWindow = new AddEditEmployeeWindow(user);
                            employeeWindow.Owner = this;
                            employeeWindow.ShowDialog();
                        }
                        else
                        {
                            MessageBox.Show($"Сотрудник с ID {entry.RelatedEntityId.Value} не найден", "Не найден",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка открытия карточки сотрудника: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Открывает окно заказа при двойном клике на номер заказа.
        /// </summary>
        private async void OrderId_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;

            if (sender is System.Windows.Controls.TextBlock textBlock &&
                textBlock.DataContext is OrderTimelineEntry entry)
            {
                try
                {
                    // ВАЖНО: GetOrdersAsync() без параметров тянет только диапазон
                    // «вчера → +30 дней», поэтому старые заказы он бы не нашёл.
                    // Берём окно ±60 дней вокруг даты события из журнала.
                    var from = entry.Timestamp.AddDays(-60);
                    var to = entry.Timestamp.AddDays(60);
                    var orders = await _apiService.GetOrdersAsync(from, to);
                    var order = orders.FirstOrDefault(o => o.Id == entry.OrderId);

                    if (order == null)
                    {
                        MessageBox.Show($"Заказ #{entry.OrderId} не найден", "Не найден",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // ViewModel создаётся ПУСТЫМ конструктором.
                    // Окно само инициализирует её вызовом Initialize(currentShift, order)
                    var viewModel = new AddEditOrderViewModel();

                    // Текущая открытая смена. Если её нет (order открыт из архива ночью
                    // или в выходной) — окно откроется в режиме просмотра:
                    // SaveButton_Click сам запретит сохранение без активной смены.
                    var currentShift = await _apiService.GetCurrentOpenShiftAsync();

                    var orderWindow = new AddEditOrderWindow(viewModel, currentShift, order)
                    {
                        Owner = this
                    };
                    orderWindow.ShowDialog();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка открытия заказа: {ex.Message}");
                }
            }
        }
    }
}