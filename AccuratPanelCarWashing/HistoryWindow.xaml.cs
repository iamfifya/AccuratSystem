using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;

namespace AccuratPanelCarWashing
{
    public partial class HistoryWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly ApiService _apiService; // Заменили SqliteDataService на ApiService

        private List<OrderDisplayItem> _box1History;
        private List<OrderDisplayItem> _box2History;
        private List<OrderDisplayItem> _box3History;
        private string _shiftSummary;

        public List<OrderDisplayItem> Box1History
        {
            get => _box1History;
            set { _box1History = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Box1History))); }
        }

        public List<OrderDisplayItem> Box2History
        {
            get => _box2History;
            set { _box2History = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Box2History))); }
        }

        public List<OrderDisplayItem> Box3History
        {
            get => _box3History;
            set { _box3History = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Box3History))); }
        }

        public string ShiftSummary
        {
            get => _shiftSummary;
            set { _shiftSummary = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShiftSummary))); }
        }

        // 1. УБИРАЕМ SqliteDataService ИЗ КОНСТРУКТОРА
        public HistoryWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            DataContext = this;

            _ = InitializeHistoryAsync();
        }

        private async Task InitializeHistoryAsync()
        {
            try
            {
                // Ищем последнюю закрытую смену, чтобы не показывать пустой экран
                var allShifts = await _apiService.GetShiftsAsync();
                var lastClosedShift = allShifts
                                        .Where(s => s.IsClosed)
                                        .OrderByDescending(s => s.Date)
                                        .FirstOrDefault();

                if (lastClosedShift != null)
                {
                    HistoryDatePicker.SelectedDate = lastClosedShift.Date;
                }
                else
                {
                    HistoryDatePicker.SelectedDate = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при инициализации истории: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadHistoryForDateAsync(DateTime date)
        {
            try
            {
                // 1. Загружаем все необходимые справочники один раз
                var allShifts = await _apiService.GetShiftsAsync();
                var allOrders = await _apiService.GetOrdersAsync();
                var allServices = await _apiService.GetServicesAsync();
                var allUsers = await _apiService.GetUsersAsync();

                // 2. Ищем закрытые смены за этот день для статистики
                var closedShiftsOnDate = allShifts.Where(s => s.Date.Date == date.Date && s.IsClosed).ToList();

                // 3. ФИЛЬТРАЦИЯ ЗАКАЗОВ И ЗАПИСЕЙ:
                // Берем заказы, которые либо привязаны к закрытым сменам этого дня,
                // либо являются предварительными записями на эту дату.
                var shiftIdsOnDate = closedShiftsOnDate.Select(s => s.Id).ToList();

                var ordersOnDate = allOrders.Where(o =>
                    (o.ShiftId.HasValue && shiftIdsOnDate.Contains(o.ShiftId.Value)) || // Заказы из закрытых смен
                    (o.IsAppointment && o.Time.Date == date.Date) // Предварительные записи на этот день
                ).ToList();

                if (!ordersOnDate.Any())
                {
                    Box1History = new List<OrderDisplayItem>();
                    Box2History = new List<OrderDisplayItem>();
                    Box3History = new List<OrderDisplayItem>();
                    ShiftSummary = "Данных за этот день нет.";
                    return;
                }

                // 4. Преобразуем в элементы для отображения
                var displayItems = ordersOnDate.Select(o => new OrderDisplayItem
                {
                    Id = o.Id,
                    CarModel = o.CarModel,
                    CarNumber = o.CarNumber,
                    Time = TimeHelper.ToMsk(o.Time), // Используем хелпер для корректного времени
                    WasherName = allUsers.FirstOrDefault(u => u.Id == o.WasherId)?.FullName ?? (o.IsAppointment ? "📅 Запись" : "Не назначен"),
                    ServicesList = string.Join(", ", (o.ServiceIds ?? new List<int>()).Select(id =>
                    {
                        var svc = allServices.FirstOrDefault(s => s.Id == id);
                        return svc != null ? svc.Name : "Unknown";
                    })),
                    FinalPrice = o.FinalPrice,
                    OriginalTotalPrice = o.OriginalTotalPrice,
                    DiscountPercent = o.DiscountPercent,
                    DiscountAmount = o.DiscountAmount,
                    ExtraCost = o.ExtraCost,
                    ExtraCostReason = o.ExtraCostReason,
                    BoxNumber = o.BoxNumber,
                    Status = o.Status,
                    PaymentMethod = o.PaymentMethod,
                    IsAppointment = o.IsAppointment
                }).OrderBy(i => i.Time).ToList();

                // 5. Распределяем по боксам
                Box1History = displayItems.Where(i => i.BoxNumber == 1).ToList();
                Box2History = displayItems.Where(i => i.BoxNumber == 2).ToList();
                Box3History = displayItems.Where(i => i.BoxNumber == 3).ToList();

                // 6. Обновляем сводку (только по реально выполненным заказам)
                var completedOrders = ordersOnDate.Where(o => o.Status == "Выполнен").ToList();
                decimal totalRevenue = completedOrders.Sum(o => o.FinalPrice);

                ShiftSummary = $"✅ Выполнено: {completedOrders.Count} | ⏳ Записей: {ordersOnDate.Count(o => o.IsAppointment)} | 💰 Выручка: {totalRevenue:N0} ₽";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке истории: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
