using AccuratSystem.Contracts.DTOs;
using AccuratPanelCWD.Models;
using AccuratPanelCWD.Services;
using AccuratPanelCWD.ViewModels;
using AccuratSystem.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ContractsUser = AccuratSystem.Contracts.Models.User;

namespace AccuratPanelCWD.Windows
{
    // Конвертер для привязки Visibility к null-значению: если объект null, то Visibility.Collapsed, иначе Visibility.Visible
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Превращает технические имена событий (StatusChanged) в человекочитаемые (🔄 Статус изменён).
    /// </summary>
    public class EventTypeRuConverter : IValueConverter
    {
        private static readonly Dictionary<string, string> Map = new Dictionary<string, string>
    {
        { "StatusChanged", "🔄 Статус изменён" },
        { "DiscountApplied", "🏷 Скидка" },
        { "ExtraCostChanged", "➕ Доплата" },
        { "PriceChanged", "💰 Цена" },
        { "BoxChanged", "🅿️ Бокс" },
        { "WasherChanged", "👤 Мойщик" },
        { "PaymentMethodChanged", "💳 Оплата" },
        { "TimeChanged", "🕐 Время" },
        { "ShiftTransferred", "📦 Перенос" },
        { "ExpenseAdded", "🧾 Расход" },
        { "ShiftOpened", "🟢 Смена открыта" },
        { "ShiftClosed", "🔴 Смена закрыта" },
        { "CashReconciliation", "🧮 X-Отчёт" }
    };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var key = value as string;
            return key != null && Map.TryGetValue(key, out var ru) ? ru : (value ?? "");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

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
                // === Филиалы ===
                var branches = await _apiService.GetBranchesAsync();
                var branchOptions = new List<KeyValuePair<string, int?>>
                {
                    new KeyValuePair<string, int?>("🌐 Все филиалы", null)
                };
                foreach (var b in branches)
                    branchOptions.Add(new KeyValuePair<string, int?>($"🏢 {b.Name}", b.Id));

                BranchFilter.ItemsSource = branchOptions;

                // Директор по умолчанию видит всю сеть, админ — только свой филиал
                if (UserPermissions.IsSuperUser(App.CurrentUser))
                {
                    BranchFilter.SelectedItem = branchOptions.First();
                }
                else
                {
                    var myBranch = branchOptions.FirstOrDefault(x => x.Value == AppSettings.CurrentBranchId);
                    // default(KeyValuePair) имеет Value = null, поэтому проверяем так:
                    BranchFilter.SelectedItem = myBranch.Value.HasValue ? myBranch : branchOptions.First();
                }

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
                    // === СОБЫТИЯ ЗАКАЗОВ ===
                    new KeyValuePair<string, string>("🏷 Скидка применена", "DiscountApplied"),
                    new KeyValuePair<string, string>("➕ Доплата изменена", "ExtraCostChanged"),
                    new KeyValuePair<string, string>("💰 Цена изменена", "PriceChanged"),
                    new KeyValuePair<string, string>("🅿️ Бокс изменён", "BoxChanged"),
                    new KeyValuePair<string, string>("👤 Мойщик изменён", "WasherChanged"),
                    new KeyValuePair<string, string>("💳 Способ оплаты", "PaymentMethodChanged"),
                    new KeyValuePair<string, string>("🕐 Время изменено", "TimeChanged"),
                    new KeyValuePair<string, string>("🔄 Статус изменён", "StatusChanged"),
                    new KeyValuePair<string, string>("📦 Заказ перенесён", "ShiftTransferred"),
                    // === СОБЫТИЯ СМЕН ===
                    new KeyValuePair<string, string>("🟢 Смена открыта", "ShiftOpened"),
                    new KeyValuePair<string, string>("🔴 Смена закрыта", "ShiftClosed"),
                    new KeyValuePair<string, string>("🧮 X-Отчёт (пересчёт кассы)", "CashReconciliation")
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

                // Директор: что выбрано в фильтре (null = все филиалы).
                // Админ: жёстко свой филиал, независимо от фильтра (защита от любопытства).
                int? branchId = UserPermissions.IsSuperUser(App.CurrentUser)
                    ? (BranchFilter.SelectedValue is int v ? v : (int?)null)
                    : AppSettings.CurrentBranchId;

                var entries = await _apiService.GetAuditLogAsync(
                    userId: userId,
                    startDate: startDate,
                    endDate: endDate,
                    entryType: entryType,
                    branchId: branchId,
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

        private async void ResetFilters_Click(object sender, RoutedEventArgs e)  // ← добавлен async
        {
            // === Филиалы ===
            var branches = await _apiService.GetBranchesAsync();
            var branchOptions = new List<KeyValuePair<string, int?>>
            {
                new KeyValuePair<string, int?>("🌐 Все филиалы", null)
            };
            foreach (var b in branches)
                branchOptions.Add(new KeyValuePair<string, int?>($"🏢 {b.Name}", b.Id));

            BranchFilter.ItemsSource = branchOptions;

            // Директор по умолчанию видит всю сеть, админ — только свой филиал
            if (UserPermissions.IsSuperUser(App.CurrentUser))
            {
                BranchFilter.SelectedItem = branchOptions.First();
            }
            else
            {
                var myBranch = branchOptions.FirstOrDefault(x => x.Value == AppSettings.CurrentBranchId);
                BranchFilter.SelectedItem = myBranch.Value.HasValue ? myBranch : branchOptions.First();
            }

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

            await LoadAuditLogAsync();  // ← await теперь легален, т.к. метод async
        }

        // === Обработчики двойного клика ===

        /// <summary>
        /// Открывает карточку сотрудника при двойном клике на имя пользователя
        /// </summary>
        private async void EmployeeName_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;

            // ИСПРАВЛЕНО: DataContext теперь AuditEntryDto, а не OrderTimelineEntry
            if (sender is System.Windows.Controls.TextBlock textBlock &&
                textBlock.DataContext is AuditEntryDto entry &&
                entry.RelatedEntityId.HasValue)
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

        /// <summary>
        /// Открывает окно заказа при двойном клике на номер заказа.
        /// </summary>
        private async void OrderId_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;

            // ИСПРАВЛЕНО: DataContext теперь AuditEntryDto
            if (sender is System.Windows.Controls.TextBlock textBlock &&
                textBlock.DataContext is AuditEntryDto entry &&
                entry.OrderId.HasValue)
            {
                try
                {
                    var from = entry.Timestamp.AddDays(-60);
                    var to = entry.Timestamp.AddDays(60);
                    var orders = await _apiService.GetOrdersAsync(from, to);
                    var order = orders.FirstOrDefault(o => o.Id == entry.OrderId.Value);

                    if (order == null)
                    {
                        MessageBox.Show($"Заказ #{entry.OrderId.Value} не найден", "Не найден",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var viewModel = new AddEditOrderViewModel();
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