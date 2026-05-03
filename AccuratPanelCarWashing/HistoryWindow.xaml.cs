using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Threading.Tasks;

namespace AccuratPanelCarWashing
{
    public partial class HistoryWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly ApiService _apiService;
        private User _currentUser;

        // Свойства для вкладок
        public bool IsDirector => _currentUser?.Role == 1;

        private ObservableCollection<BranchTabItem> _branchTabs = new ObservableCollection<BranchTabItem>();
        public ObservableCollection<BranchTabItem> BranchTabs
        {
            get => _branchTabs;
            set { _branchTabs = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BranchTabs))); }
        }

        private BranchTabItem _selectedBranchTab;
        public BranchTabItem SelectedBranchTab
        {
            get => _selectedBranchTab;
            set { _selectedBranchTab = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedBranchTab))); }
        }

        private string _shiftSummary;
        public string ShiftSummary
        {
            get => _shiftSummary;
            set { _shiftSummary = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShiftSummary))); }
        }

        // Конструктор теперь принимает User
        public HistoryWindow(User currentUser)
        {
            InitializeComponent();
            _apiService = new ApiService();
            _currentUser = currentUser;
            DataContext = this;

            _ = InitializeHistoryAsync();
        }

        private async Task InitializeHistoryAsync()
        {
            try
            {
                // 1. ЗАГРУЖАЕМ ФИЛИАЛЫ И ФОРМИРУЕМ ВКЛАДКИ
                var allBranches = await _apiService.GetBranchesAsync();
                BranchTabs.Clear();

                if (IsDirector)
                {
                    foreach (var b in allBranches)
                    {
                        BranchTabs.Add(new BranchTabItem
                        {
                            BranchId = b.Id,
                            BranchName = b.Name,
                            BranchWorkZones = GenerateZonesForBranch(b)
                        });
                    }
                }
                else
                {
                    var myBranch = allBranches.FirstOrDefault(b => b.Id == AppSettings.CurrentBranchId);
                    if (myBranch != null)
                    {
                        BranchTabs.Add(new BranchTabItem
                        {
                            BranchId = myBranch.Id,
                            BranchName = myBranch.Name,
                            BranchWorkZones = GenerateZonesForBranch(myBranch)
                        });
                    }
                }

                if (BranchTabs.Any())
                    SelectedBranchTab = BranchTabs.First();

                // 2. ИЩЕМ ДАТУ ПОСЛЕДНЕЙ СМЕНЫ
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

        private ObservableCollection<WorkZone> GenerateZonesForBranch(Branch branch)
        {
            var zones = new ObservableCollection<WorkZone>();
            for (int i = 1; i <= branch.WashBaysCount; i++)
                zones.Add(new WorkZone { ZoneNumber = i, ZoneName = $"БОКС {i}", Department = "Wash" });
            for (int i = 1; i <= branch.ServiceLiftsCount; i++)
                zones.Add(new WorkZone { ZoneNumber = i, ZoneName = $"ПОДЪЕМНИК {i}", Department = "Service" });
            return zones;
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
                var allOrders = await _apiService.GetOrdersAsync();
                var allServices = await _apiService.GetServicesAsync();
                var allUsers = await _apiService.GetUsersAsync();

                var closedShiftsOnDate = allShifts.Where(s => s.Date.Date == date.Date && s.IsClosed).ToList();
                var shiftIdsOnDate = closedShiftsOnDate.Select(s => s.Id).ToList();

                var ordersOnDate = allOrders.Where(o =>
                    (o.ShiftId.HasValue && shiftIdsOnDate.Contains(o.ShiftId.Value)) ||
                    (o.IsAppointment && o.Time.Date == date.Date)
                ).ToList();

                if (!ordersOnDate.Any())
                {
                    // Очищаем списки во всех зонах
                    foreach (var tab in BranchTabs)
                        foreach (var zone in tab.BranchWorkZones)
                            zone.Orders.Clear();

                    ShiftSummary = "Данных за этот день нет.";
                    return;
                }

                // Маппинг заказов
                var displayItems = ordersOnDate.Select(o => new OrderDisplayItem
                {
                    Id = o.Id,
                    BranchId = o.BranchId,     // ⚡ ВАЖНО для вкладок
                    Department = o.Department, // ⚡ ВАЖНО для вкладок
                    CarModel = o.CarModel,
                    CarNumber = o.CarNumber,
                    Time = TimeHelper.ToMsk(o.Time),
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

                // ⚡ РАСПРЕДЕЛЕНИЕ ПО ВКЛАДКАМ И БОКСАМ
                foreach (var tab in BranchTabs)
                {
                    foreach (var zone in tab.BranchWorkZones)
                    {
                        var ordersForZone = displayItems.Where(i =>
                            i.BranchId == tab.BranchId &&
                            i.BoxNumber == zone.ZoneNumber &&
                            i.Department == zone.Department).ToList();

                        zone.Orders.Clear();
                        foreach (var o in ordersForZone)
                        {
                            zone.Orders.Add(o);
                        }
                    }
                }

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