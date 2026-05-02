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

        private void HistoryDatePicker_SelectedDateChanged(object sender, DateTime? e)
        {
            if (e.HasValue)
            {
                _ = LoadHistoryForDateAsync(e.Value);
            }
        }

        private async Task LoadHistoryForDateAsync(DateTime date)
        {
            try
            {
                var allShifts = await _apiService.GetShiftsAsync();
                var closedShiftsOnDate = allShifts.Where(s => s.Date.Date == date.Date && s.IsClosed).ToList();

                if (!closedShiftsOnDate.Any())
                {
                    Box1History = new List<OrderDisplayItem>();
                    Box2History = new List<OrderDisplayItem>();
                    Box3History = new List<OrderDisplayItem>();
                    ShiftSummary = "В этот день закрытых смен нет.";
                    return;
                }

                var allOrdersOnDate = new List<CarWashOrder>();

                // Чтобы не дергать API в цикле, загрузим все заказы и отфильтруем
                var allOrders = await _apiService.GetOrdersAsync();

                foreach (var shift in closedShiftsOnDate)
                {
                    var shiftOrders = allOrders.Where(o => o.ShiftId == shift.Id).ToList();
                    allOrdersOnDate.AddRange(shiftOrders);
                }

                var allServices = await _apiService.GetServicesAsync();
                var allUsers = await _apiService.GetUsersAsync();

                var displayItems = allOrdersOnDate.Select(o => new OrderDisplayItem
                {
                    Id = o.Id,
                    CarModel = o.CarModel,
                    CarNumber = o.CarNumber,
                    Time = o.Time,
                    WasherName = allUsers.FirstOrDefault(u => u.Id == o.WasherId)?.FullName ?? "Не назначен",
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

                Box1History = displayItems.Where(i => i.BoxNumber == 1).ToList();
                Box2History = displayItems.Where(i => i.BoxNumber == 2).ToList();
                Box3History = displayItems.Where(i => i.BoxNumber == 3).ToList();

                var completedOrders = allOrdersOnDate.Where(o => o.Status == "Выполнен").ToList();
                decimal totalRevenue = completedOrders.Sum(o => o.FinalPrice);

                ShiftSummary = $"✅ Машин: {completedOrders.Count} | 💰 Выручка: {totalRevenue:N0} ₽";
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
