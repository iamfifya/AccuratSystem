using AccuratPanelCWD.Models;
using AccuratPanelCWD.Services;
using AccuratSystem.Contracts.Enums;
using AccuratSystem.Contracts.Models;
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
using ContractsEmployeeReport = AccuratSystem.Contracts.Models.EmployeeReport;
using ContractsShiftReport = AccuratSystem.Contracts.Models.ShiftReport;
using WpfUser = AccuratPanelCWD.Models.User;

namespace AccuratPanelCWD
{
    public partial class CustomReportWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly ApiService _apiService;
        private readonly WpfUser _currentUser;
        public Func<double, string> YFormatter { get; } = value => value.ToString("#,0");

        private AccuratSystem.Contracts.Models.CustomPeriodReport _lastGeneratedReport;
        public bool IsDirector => UserPermissions.IsSuperUser(_currentUser);

        public SeriesCollection RevenueSeries { get; set; }
        public SeriesCollection ShareSeries { get; set; }
        public SeriesCollection HourlyLoadSeries { get; set; }
        public SeriesCollection ExpenseCategoriesSeries { get; set; }
        public string[] Labels { get; set; }
        public string[] HourLabels { get; set; }

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
                ReportContent.Visibility = Visibility.Collapsed;
            }
        }

        public CustomReportWindow(WpfUser user)
        {
            InitializeComponent();
            _apiService = new ApiService();
            _currentUser = user;
            DataContext = this;

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
                if (IsDirector) BranchTabs.Add(new BranchTabItem { BranchId = 0, BranchName = "🌐 Вся сеть" });
                foreach (var b in branches) BranchTabs.Add(new BranchTabItem { BranchId = b.Id, BranchName = b.Name });
                if (BranchTabs.Any()) SelectedBranchTab = BranchTabs.First();
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

                var periodReports = await _apiService.GetShiftReportsAsync(branchId, TimeHelper.ToUtc(start), TimeHelper.ToUtc(end));

                if (!periodReports.Any())
                {
                    MessageBox.Show("Нет данных за выбранный период", "Инфо", MessageBoxButton.OK, MessageBoxImage.Information);
                    ReconciliationSummaryText.Visibility = Visibility.Collapsed;
                    return;
                }

                var reconSummary = await _apiService.GetReconciliationsSummaryAsync(branchId, TimeHelper.ToUtc(start), TimeHelper.ToUtc(end));
                if (reconSummary.TotalShifts > 0)
                {
                    ReconciliationSummaryText.Visibility = Visibility.Visible;
                    if (reconSummary.ReconciledShifts == reconSummary.TotalShifts)
                    {
                        ReconciliationSummaryText.Text = $"🧮 Касса пересчитана во всех {reconSummary.TotalShifts} сменах. Суммарная разница: {reconSummary.TotalDifference:+0;-0;0} ₽";
                        ReconciliationSummaryText.Foreground = TryFindResource(reconSummary.TotalDifference == 0 ? "AccentGreen" : "AccentRed") as Brush
                            ?? new SolidColorBrush(reconSummary.TotalDifference == 0 ? Colors.Green : Colors.Red);
                    }
                    else
                    {
                        ReconciliationSummaryText.Text = $"⚠️ X-отчёт проведён только в {reconSummary.ReconciledShifts} из {reconSummary.TotalShifts} смен. Суммарная разница: {reconSummary.TotalDifference:+0;-0;0} ₽";
                        ReconciliationSummaryText.Foreground = TryFindResource("AccentOrange") as Brush ?? new SolidColorBrush(Colors.Orange);
                    }
                }
                else
                {
                    ReconciliationSummaryText.Visibility = Visibility.Collapsed;
                }

                var clientStats = await _apiService.GetClientsStatsAsync(branchId, TimeHelper.ToUtc(start), TimeHelper.ToUtc(end));

                decimal totalRev = periodReports.Sum(r => r.TotalRevenue);
                decimal netProfit = periodReports.Sum(r => r.NetProfit);
                decimal avgCheck = clientStats.UniqueClients > 0 ? totalRev / periodReports.Sum(r => r.TotalCars) : 0;

                TotalRevenueText.Text = $"{totalRev:N0} ₽";
                NetProfitText.Text = $"{netProfit:N0} ₽";
                TotalCarsText.Text = periodReports.Sum(r => r.TotalCars).ToString();
                NewClientsText.Text = clientStats.NewClients.ToString();

                WashRevenueText.Text = $"Выручка: {periodReports.Sum(r => r.WashTotalRevenue):N0} ₽";
                WashCarsText.Text = $"Заказов: {periodReports.Sum(r => r.WashTotalCars)}";
                WashProfitText.Text = $"Прибыль: {periodReports.Sum(r => r.WashNetProfit):N0} ₽";
                WashProfitText.Foreground = TryFindResource("ReportWashProfit") as Brush ?? new SolidColorBrush(Colors.Green);

                ServiceRevenueText.Text = $"Выручка: {periodReports.Sum(r => r.ServiceTotalRevenue):N0} ₽";
                ServiceCarsText.Text = $"Заказов: {periodReports.Sum(r => r.ServiceTotalCars)}";
                ServiceProfitText.Text = $"Прибыль: {periodReports.Sum(r => r.ServiceNetProfit):N0} ₽";
                ServiceProfitText.Foreground = TryFindResource("ReportServiceProfit") as Brush ?? new SolidColorBrush(Color.FromRgb(26, 82, 118));

                CashTotalText.Text = $"{periodReports.Sum(r => r.CashAmount):N0} ₽ ({periodReports.Sum(r => r.CashCount)} шт.)";
                CardTotalText.Text = $"{periodReports.Sum(r => r.CardAmount):N0} ₽ ({periodReports.Sum(r => r.CardCount)} шт.)";
                TransferTotalText.Text = $"{periodReports.Sum(r => r.TransferAmount):N0} ₽ ({periodReports.Sum(r => r.TransferCount)} шт.)";
                QrTotalText.Text = $"{periodReports.Sum(r => r.QrAmount):N0} ₽ ({periodReports.Sum(r => r.QrCount)} шт.)";

                AvgCheckText.Text = $"{avgCheck:N0} ₽";
                decimal totalDiscounts = periodReports.Sum(r => r.TotalDiscountAmount);
                DiscountsText.Text = $"{totalDiscounts:N0} ₽";
                RetentionText.Text = $"{clientStats.RetentionRate:N1}%";
                RepeatClientsText.Text = clientStats.RepeatClients.ToString();

                var allTopServices = periodReports
                    .SelectMany(r => r.TopServices)
                    .GroupBy(s => s.ServiceName)
                    .Select(g => new ServiceAnalytics
                    {
                        ServiceName = g.Key,
                        Count = g.Sum(s => s.Count),
                        TotalRevenue = g.Sum(s => s.TotalRevenue)
                    })
                    .OrderByDescending(s => s.Count)
                    .ThenByDescending(s => s.TotalRevenue)
                    .Take(5)
                    .ToList();

                TopServicesList.ItemsSource = allTopServices;

                // ГРАФИКИ LiveCharts 
                RevenueSeries = new SeriesCollection {
                    new LineSeries {
                        Title = "Выручка",
                        Values = new ChartValues<decimal>(periodReports.OrderBy(r => r.Date).Select(r => r.TotalRevenue)),
                        PointGeometry = DefaultGeometries.Circle, PointGeometrySize = 10
                    }
                };

                ShareSeries = new SeriesCollection {
                    new PieSeries { Title = "Мойка", Values = new ChartValues<decimal> { periodReports.Sum(r => r.WashTotalRevenue) }, DataLabels = true },
                    new PieSeries { Title = "Сервис", Values = new ChartValues<decimal> { periodReports.Sum(r => r.ServiceTotalRevenue) }, DataLabels = true }
                };

                Labels = periodReports.OrderBy(r => r.Date).Select(r => r.Date.ToString("dd.MM")).ToArray();

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RevenueSeries)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShareSeries)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Labels)));

                var allExpenses = periodReports
                    .SelectMany(r => r.ExpensesByCategory)
                    .GroupBy(e => e.Category)
                    .Select(g => new ExpenseCategoryReport
                    {
                        Category = g.Key,
                        TotalAmount = g.Sum(e => e.TotalAmount),
                        Count = g.Sum(e => e.Count),
                        Percentage = periodReports.Sum(r => r.TotalExpenses) > 0
                            ? Math.Round(g.Sum(e => e.TotalAmount) / periodReports.Sum(r => r.TotalExpenses) * 100, 1)
                            : 0
                    })
                    .OrderByDescending(e => e.TotalAmount)
                    .ToList();

                ExpensesList.ItemsSource = allExpenses;
                TotalExpensesText.Text = $"{periodReports.Sum(r => r.TotalExpenses):N0} ₽";

                var hourlyLoad = Enumerable.Range(0, 24)
                    .Select(hour => new HourlyLoad
                    {
                        Hour = hour,
                        HourLabel = $"{hour:D2}:00",
                        OrdersCount = periodReports.Sum(r => r.HourlyLoad.FirstOrDefault(h => h.Hour == hour)?.OrdersCount ?? 0),
                        Revenue = periodReports.Sum(r => r.HourlyLoad.FirstOrDefault(h => h.Hour == hour)?.Revenue ?? 0)
                    })
                    .ToList();

                HourlyLoadList.ItemsSource = hourlyLoad;

                var expectedCash = periodReports.Sum(r => r.ExpectedCashBalance);
                var actualCash = periodReports.Sum(r => r.ActualCashBalance);
                var cashDifference = periodReports.Sum(r => r.CashBalanceDifference);

                ExpectedCashText.Text = $"{expectedCash:N0} ₽";
                ActualCashText.Text = $"{actualCash:N0} ₽";
                CashDifferenceText.Text = $"{cashDifference:+0;-0;0} ₽";
                CashDifferenceText.Foreground = TryFindResource(cashDifference == 0 ? "AccentGreen" : "AccentRed") as Brush
                    ?? new SolidColorBrush(cashDifference == 0 ? Colors.Green : Colors.Red);

                RevenueSeries = new SeriesCollection
                {
                    new LineSeries
                    {
                        Title = "Выручка",
                        Values = new ChartValues<decimal>(periodReports.OrderBy(r => r.Date).Select(r => r.TotalRevenue)),
                        PointGeometry = DefaultGeometries.Circle, PointGeometrySize = 10
                    }
                };

                ShareSeries = new SeriesCollection
                {
                    new PieSeries { Title = "Мойка", Values = new ChartValues<decimal> { periodReports.Sum(r => r.WashTotalRevenue) }, DataLabels = true },
                    new PieSeries { Title = "Сервис", Values = new ChartValues<decimal> { periodReports.Sum(r => r.ServiceTotalRevenue) }, DataLabels = true }
                };

                HourlyLoadSeries = new SeriesCollection
                {
                    new ColumnSeries
                    {
                        Title = "Заказы",
                        Values = new ChartValues<int>(hourlyLoad.Select(h => h.OrdersCount)),
                        DataLabels = true,
                        LabelPoint = p => p.Instance.ToString()
                    }
                };
                HourLabels = hourlyLoad.Select(h => h.HourLabel).ToArray();

                ExpenseCategoriesSeries = new SeriesCollection();
                var colors = new[] {
                    TryFindResource("AccentRed") as Brush ?? new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                    TryFindResource("AccentOrange") as Brush ?? new SolidColorBrush(Color.FromRgb(230, 126, 34)),
                    TryFindResource("AccentYellow") as Brush ?? new SolidColorBrush(Color.FromRgb(241, 196, 15)),
                    TryFindResource("AccentGreen") as Brush ?? new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                    TryFindResource("AccentBlue") as Brush ?? new SolidColorBrush(Color.FromRgb(52, 152, 219))
                };

                for (int i = 0; i < Math.Min(allExpenses.Count, 5); i++)
                {
                    var expense = allExpenses[i];
                    ExpenseCategoriesSeries.Add(new PieSeries
                    {
                        Title = expense.Category,
                        Values = new ChartValues<decimal> { expense.TotalAmount },
                        DataLabels = true,
                        Fill = colors[i % colors.Length]
                    });
                }

                Labels = periodReports.OrderBy(r => r.Date).Select(r => r.Date.ToString("dd.MM")).ToArray();

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RevenueSeries)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShareSeries)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HourlyLoadSeries)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExpenseCategoriesSeries)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Labels)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HourLabels)));

                _lastGeneratedReport = new AccuratSystem.Contracts.Models.CustomPeriodReport
                {
                    StartDate = start,
                    EndDate = end,
                    BranchName = SelectedBranchTab?.BranchName ?? "Вся сеть",
                    TotalCars = periodReports.Sum(r => r.TotalCars),
                    TotalRevenue = totalRev,
                    TotalWasherEarnings = periodReports.Sum(r => r.TotalWasherEarnings),
                    TotalCompanyEarnings = periodReports.Sum(r => r.TotalCompanyEarnings),
                    TotalExpenses = periodReports.Sum(r => r.TotalExpenses),
                    CashCount = periodReports.Sum(r => r.CashCount),
                    CashAmount = periodReports.Sum(r => r.CashAmount),
                    CardCount = periodReports.Sum(r => r.CardCount),
                    CardAmount = periodReports.Sum(r => r.CardAmount),
                    TransferCount = periodReports.Sum(r => r.TransferCount),
                    TransferAmount = periodReports.Sum(r => r.TransferAmount),
                    QrCount = periodReports.Sum(r => r.QrCount),
                    QrAmount = periodReports.Sum(r => r.QrAmount),
                    ExpensesByCategory = allExpenses,
                    HourlyLoad = hourlyLoad,
                    DailyReports = periodReports.Select(r => new AccuratSystem.Contracts.Models.DailyReportSummary
                    {
                        Date = r.Date,
                        TotalCars = r.TotalCars,
                        TotalRevenue = r.TotalRevenue,
                        TotalWasherEarnings = r.TotalWasherEarnings,
                        TotalCompanyEarnings = r.TotalCompanyEarnings
                    }).ToList(),
                    EmployeesWork = periodReports.SelectMany(r => r.EmployeesWork)
                        .GroupBy(e => e.EmployeeId)
                        .Select(g => new AccuratSystem.Contracts.Models.EmployeeReport
                        {
                            EmployeeName = g.First().EmployeeName,
                            CarsWashed = g.Sum(x => x.CarsWashed),
                            Earnings = g.Sum(x => x.Earnings),
                            Advances = g.Sum(x => x.Advances)
                        }).ToList()
                };

                ApplyThemeToCharts();
                ReportContent.Visibility = Visibility.Visible;
                EmployeesSalaryList.ItemsSource = _lastGeneratedReport.EmployeesWork;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            finally { this.IsEnabled = true; }
        }

        private void ApplyThemeToCharts()
        {
            if (RevenueSeries != null && RevenueSeries.Count > 0)
            {
                var lineSeries = RevenueSeries[0] as LineSeries;
                if (lineSeries != null)
                {
                    var accentGreen = TryFindResource("AccentGreen") as Brush ?? new SolidColorBrush(Color.FromRgb(39, 174, 96));
                    lineSeries.Fill = accentGreen;
                    lineSeries.StrokeThickness = 3;
                    lineSeries.PointForeground = Brushes.White;
                }
            }

            if (ShareSeries != null)
            {
                var colors = new[] {
                    TryFindResource("AccentBlue") as Brush ?? new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                    TryFindResource("AccentOrange") as Brush ?? new SolidColorBrush(Color.FromRgb(230, 126, 34))
                };
                for (int i = 0; i < ShareSeries.Count && i < colors.Length; i++)
                {
                    var pieSeries = ShareSeries[i] as PieSeries;
                    if (pieSeries != null)
                    {
                        pieSeries.Fill = colors[i];
                        pieSeries.Foreground = TryFindResource("TextMain") as Brush ?? Brushes.Black;
                    }
                }
            }

            if (HourlyLoadSeries != null && HourlyLoadSeries.Count > 0)
            {
                var columnSeries = HourlyLoadSeries[0] as ColumnSeries;
                if (columnSeries != null)
                {
                    var accentBlue = TryFindResource("AccentBlue") as Brush ?? new SolidColorBrush(Color.FromRgb(52, 152, 219));
                    columnSeries.Fill = accentBlue;
                    columnSeries.Foreground = TryFindResource("TextMain") as Brush ?? Brushes.Black;
                }
            }
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();


        // ═══════════════════════════════════════════════════════
        //  СРАВНЕНИЕ ПЕРИОДОВ (ОВЕРЛЕЙ)
        // ═══════════════════════════════════════════════════════

        private PeriodComparisonOverlay _comparisonOverlay;

        private void ComparePeriodsButton_Click(object sender, RoutedEventArgs e)
        {
            // Получаем текущие параметры из окна
            DateTime curStart = StartDatePicker.SelectedDate ?? DateTime.Now.AddDays(-7);
            DateTime curEnd = EndDatePicker.SelectedDate ?? DateTime.Now;
            int branchId = SelectedBranchTab?.BranchId ?? 0;

            // Создаём оверлей
            _comparisonOverlay = new PeriodComparisonOverlay(branchId, curStart, curEnd);
            _comparisonOverlay.Closed += OnComparisonOverlayClosed;

            // Добавляем в контейнер
            OverlayContainer.Children.Clear();
            OverlayContainer.Children.Add(_comparisonOverlay);
            OverlayContainer.Visibility = Visibility.Visible;
        }

        private void OnComparisonOverlayClosed()
        {
            if (_comparisonOverlay != null)
            {
                _comparisonOverlay.Closed -= OnComparisonOverlayClosed;
                _comparisonOverlay = null;
            }
            OverlayContainer.Children.Clear();
            OverlayContainer.Visibility = Visibility.Collapsed;
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastGeneratedReport == null)
            {
                MessageBox.Show("Сначала сформируйте отчет!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                DefaultExt = "xlsx",
                FileName = $"Интервальный_Отчет_{_lastGeneratedReport.StartDate:dd.MM.yyyy}-{_lastGeneratedReport.EndDate:dd.MM.yyyy}"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    ExcelExporter.ExportCustomPeriodReport(_lastGeneratedReport, saveDialog.FileName);
                    MessageBox.Show($"Отчет успешно экспортирован!\n\nФайл сохранен: {saveDialog.FileName}", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Ошибка экспорта", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}