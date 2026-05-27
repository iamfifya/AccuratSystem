using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

// Алиасы для предотвращения конфликтов
using WpfUser = AccuratPanelCarWashing.Models.User;
using ContractsShiftReport = AccuratSystem.Contracts.Models.ShiftReport;
using ContractsEmployeeReport = AccuratSystem.Contracts.Models.EmployeeReport;

namespace AccuratPanelCarWashing
{
    public partial class CustomReportWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly ApiService _apiService;
        private readonly WpfUser _currentUser;

        public bool IsDirector => _currentUser?.Role == 1;

        // Данные для графиков
        public SeriesCollection RevenueSeries { get; set; }
        public SeriesCollection ShareSeries { get; set; }
        public string[] Labels { get; set; }

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
            set
            {
                _selectedBranchTab = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedBranchTab)));
                // Скрываем контент при смене филиала, чтобы пользователь нажал "Сформировать" заново
                ReportContent.Visibility = Visibility.Collapsed;
            }
        }

        public CustomReportWindow(WpfUser user)
        {
            InitializeComponent();
            _apiService = new ApiService();
            _currentUser = user;
            DataContext = this;

            // Дефолтные даты: за последние 7 дней
            StartDatePicker.SelectedDate = DateTime.Now.AddDays(-7);
            EndDatePicker.SelectedDate = DateTime.Now;

            _ = InitializeTabsAsync();
        }

        private async Task InitializeTabsAsync()
        {
            try
            {
                var branches = await _apiService.GetBranchesAsync();
                BranchTabs.Clear();

                if (IsDirector)
                    BranchTabs.Add(new BranchTabItem { BranchId = 0, BranchName = "🌐 Вся сеть" });

                foreach (var b in branches)
                    BranchTabs.Add(new BranchTabItem { BranchId = b.Id, BranchName = b.Name });

                if (BranchTabs.Any())
                    SelectedBranchTab = BranchTabs.First();
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка загрузки филиалов: {ex.Message}"); }
        }

        private async void GenerateReportButton_Click(object sender, RoutedEventArgs args)
        {
            try
            {
                this.IsEnabled = false;
                DateTime start = StartDatePicker.SelectedDate ?? DateTime.Now.AddDays(-7);
                DateTime end = EndDatePicker.SelectedDate ?? DateTime.Now;

                int branchId = SelectedBranchTab?.BranchId ?? 0;

                // Загрузка данных через API
                var periodReports = await _apiService.GetShiftReportsAsync(branchId, TimeHelper.ToUtc(start), TimeHelper.ToUtc(end));
                var clientStats = await _apiService.GetClientsStatsAsync(branchId, TimeHelper.ToUtc(start), TimeHelper.ToUtc(end));
                var transactions = await _apiService.GetTransactionsByDateRangeAsync(branchId, TimeHelper.ToUtc(start), TimeHelper.ToUtc(end));

                if (periodReports == null || !periodReports.Any())
                {
                    MessageBox.Show("Нет данных за выбранный период и филиал", "Инфо", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 1. РАСЧЕТЫ ОБЩИЕ
                decimal totalRev = periodReports.Sum(r => r.TotalRevenue);
                // Чистая прибыль = Прибыль компании - Расходы
                decimal netProfit = periodReports.Sum(r => r.TotalCompanyEarnings) - transactions.Where(t => t.Type == "Расход").Sum(t => t.Amount);

                TotalRevenueText.Text = $"{totalRev:N0} ₽";
                NetProfitText.Text = $"{netProfit:N0} ₽";
                TotalCarsText.Text = periodReports.Sum(r => r.TotalCars).ToString();
                NewClientsText.Text = clientStats?.NewClients.ToString() ?? "0";

                // 2. СЕКЦИИ ДЕПАРТАМЕНТОВ
                WashRevenueText.Text = $"Выручка: {periodReports.Sum(r => r.WashTotalRevenue):N0} ₽";
                WashCarsText.Text = $"Заказов: {periodReports.Sum(r => r.WashTotalCars)}";
                WashProfitText.Text = $"Прибыль: {periodReports.Sum(r => r.WashNetProfit):N0} ₽";
                WashProfitText.Foreground = new SolidColorBrush(Colors.Green);

                ServiceRevenueText.Text = $"Выручка: {periodReports.Sum(r => r.ServiceTotalRevenue):N0} ₽";
                ServiceCarsText.Text = $"Заказов: {periodReports.Sum(r => r.ServiceTotalCars)}";
                ServiceProfitText.Text = $"Прибыль: {periodReports.Sum(r => r.ServiceNetProfit):N0} ₽";
                ServiceProfitText.Foreground = new SolidColorBrush(Colors.DarkBlue);

                // 3. СПОСОБЫ ОПЛАТЫ
                CashTotalText.Text = $"{periodReports.Sum(r => r.CashAmount):N0} ₽ ({periodReports.Sum(r => r.CashCount)} шт.)";
                CardTotalText.Text = $"{periodReports.Sum(r => r.CardAmount):N0} ₽ ({periodReports.Sum(r => r.CardCount)} шт.)";
                TransferTotalText.Text = $"{periodReports.Sum(r => r.TransferAmount):N0} ₽ ({periodReports.Sum(r => r.TransferCount)} шт.)";
                QrTotalText.Text = $"{periodReports.Sum(r => r.QrAmount):N0} ₽ ({periodReports.Sum(r => r.QrCount)} шт.)";

                // 4. ГРАФИКИ
                var sortedReports = periodReports.OrderBy(r => r.Date).ToList();
                RevenueSeries = new SeriesCollection {
                    new LineSeries {
                        Title = "Выручка",
                        Values = new ChartValues<decimal>(sortedReports.Select(r => r.TotalRevenue)),
                        PointGeometry = DefaultGeometries.Circle, PointGeometrySize = 8
                    }
                };

                ShareSeries = new SeriesCollection {
                    new PieSeries { Title = "Мойка", Values = new ChartValues<decimal> { periodReports.Sum(r => r.WashTotalRevenue) }, DataLabels = true },
                    new PieSeries { Title = "Сервис", Values = new ChartValues<decimal> { periodReports.Sum(r => r.ServiceTotalRevenue) }, DataLabels = true }
                };

                Labels = sortedReports.Select(r => r.Date.ToString("dd.MM")).ToArray();

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RevenueSeries)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShareSeries)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Labels)));

                // 5. ЗАРПЛАТНАЯ ВЕДОМОСТЬ (Группировка по сотрудникам за весь период)
                var salaryData = periodReports.SelectMany(r => r.EmployeesWork)
                    .GroupBy(e => e.EmployeeId)
                    .Select(g => new ContractsEmployeeReport
                    {
                        EmployeeName = g.First().EmployeeName,
                        CarsWashed = g.Sum(x => x.CarsWashed),
                        Earnings = g.Sum(x => x.Earnings),
                        Advances = g.Sum(x => x.Advances)
                    }).ToList();

                EmployeesSalaryList.ItemsSource = salaryData;

                ReportContent.Visibility = Visibility.Visible;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            finally { this.IsEnabled = true; }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
        private void ExportButton_Click(object sender, RoutedEventArgs e) { /* Вызов вашего ExcelExporter */ }
    }
}
