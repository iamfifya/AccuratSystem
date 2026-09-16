using AccuratPanelCWD.Services;
using AccuratSystem.Contracts.DTOs;
using AccuratSystem.Contracts.Models;
using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace AccuratPanelCWD
{
    public partial class PeriodComparisonOverlay : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly ApiService _apiService;

        // Контекст
        private readonly int _branchId;
        private readonly DateTime _currentStart;
        private readonly DateTime _currentEnd;

        // Результат
        private PeriodComparisonFull _comparisonResult;

        // Форматтеры
        public Func<double, string> YFormatter { get; } = value => value.ToString("#,0");
        public Func<double, string> PercentFormatter { get; } = value => value.ToString("#,0") + "%";

        // === Графики: Выручка ===
        public SeriesCollection RevenueByDaySeries { get; set; }
        public SeriesCollection RevenueByPercentSeries { get; set; }

        // === Графики: Заказы ===
        public SeriesCollection CarsByDaySeries { get; set; }
        public SeriesCollection CarsByPercentSeries { get; set; }

        // === Графики: Средний чек ===
        public SeriesCollection AvgCheckByDaySeries { get; set; }
        public SeriesCollection AvgCheckByPercentSeries { get; set; }

        // === Графики: Загруженность по часам ===
        public SeriesCollection HourlyLoadSeries { get; set; }
        public SeriesCollection HourlyLoadPercentSeries { get; set; }

        // === Метки осей ===
        public string[] CurrentDayLabels { get; set; }
        public string[] PercentDayLabels { get; set; }
        public string[] HourLabels { get; set; }

        // Событие закрытия
        public event Action Closed;

        public PeriodComparisonOverlay(int branchId, DateTime currentStart, DateTime currentEnd)
        {
            InitializeComponent();
            DataContext = this;

            _apiService = new ApiService();
            _branchId = branchId;
            _currentStart = currentStart;
            _currentEnd = currentEnd;

            // Заполняем текущий период (подхватывается из основного окна)
            CurrentStartDatePicker.SelectedDate = currentStart;
            CurrentEndDatePicker.SelectedDate = currentEnd;

            // Прошлый период по умолчанию: предыдущий месяц
            PreviousStartDatePicker.SelectedDate = currentStart.AddMonths(-1);
            PreviousEndDatePicker.SelectedDate = currentEnd.AddMonths(-1);

            // Метки часов
            HourLabels = Enumerable.Range(0, 24).Select(h => $"{h:D2}:00").ToArray();

            // Обработчик нажатия клавиш в окне
            Loaded += (s, e) =>
            {
                var win = Window.GetWindow(this);
                if (win != null) win.PreviewKeyDown += OnWindowPreviewKeyDown;
            };

            Unloaded += (s, e) =>
            {
                var win = Window.GetWindow(this);
                if (win != null) win.PreviewKeyDown -= OnWindowPreviewKeyDown;
            };
        }

        // ═══════════════════════════════════════════════════════
        //  КНОПКА СРАВНИТЬ
        // ═══════════════════════════════════════════════════════

        private async void CompareButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Блокируем кнопку
                CompareButton.IsEnabled = false;
                CompareButton.Content = "⏳ Загрузка...";

                // Показываем скелетон, скрываем результат
                LoadingSkeleton.Visibility = Visibility.Visible;
                ComparisonResult.Visibility = Visibility.Collapsed;
                WarningText.Visibility = Visibility.Collapsed;

                DateTime curStart = CurrentStartDatePicker.SelectedDate ?? _currentStart;
                DateTime curEnd = CurrentEndDatePicker.SelectedDate ?? _currentEnd;
                DateTime prevStart = PreviousStartDatePicker.SelectedDate ?? curStart.AddMonths(-1);
                DateTime prevEnd = PreviousEndDatePicker.SelectedDate ?? curEnd.AddMonths(-1);

                // Вызываем API
                _comparisonResult = await _apiService.GetPeriodComparisonFullAsync(
                    _branchId,
                    TimeHelper.ToUtc(curStart), TimeHelper.ToUtc(curEnd),
                    TimeHelper.ToUtc(prevStart), TimeHelper.ToUtc(prevEnd));

                // Предупреждение
                if (!string.IsNullOrWhiteSpace(_comparisonResult.Warning))
                {
                    WarningText.Text = $"⚠️ {_comparisonResult.Warning}";
                    WarningText.Visibility = Visibility.Visible;
                }

                // Заполняем все секции
                FillKpiCards(_comparisonResult.Deltas, _comparisonResult.CurrentPeriod, _comparisonResult.PreviousPeriod);
                FillDailyCharts(_comparisonResult.DailyData);
                FillHourlyLoadCharts(_comparisonResult.CurrentHourlyLoad, _comparisonResult.PreviousHourlyLoad);
                FillDepartments(_comparisonResult.DepartmentsComparison);
                FillServicesComparison(_comparisonResult.ServicesComparison);
                FillExpensesComparison(_comparisonResult.ExpensesComparison);
                FillClientsComparison(_comparisonResult.ClientsComparison);
                FillFotComparison(_comparisonResult.FotComparison);

                // Показываем результат
                LoadingSkeleton.Visibility = Visibility.Collapsed;
                ComparisonResult.Visibility = Visibility.Visible;
                ExportButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сравнении периодов: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CompareButton.IsEnabled = true;
                CompareButton.Content = "🚀 Сравнить";
            }
        }

        // ═══════════════════════════════════════════════════════
        //  ПРЕСЕТЫ
        // ═══════════════════════════════════════════════════════

        private void Preset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string preset)
            {
                DateTime now = DateTime.Now;
                DateTime curStart, curEnd, prevStart, prevEnd;

                switch (preset)
                {
                    case "Week":
                        curEnd = now;
                        curStart = now.AddDays(-7);
                        prevEnd = curStart.AddDays(-1);
                        prevStart = prevEnd.AddDays(-7);
                        break;
                    case "Month":
                        curEnd = now;
                        curStart = now.AddMonths(-1);
                        prevEnd = curStart.AddDays(-1);
                        prevStart = prevEnd.AddMonths(-1);
                        break;
                    case "Quarter":
                        curEnd = now;
                        curStart = now.AddMonths(-3);
                        prevEnd = curStart.AddDays(-1);
                        prevStart = prevEnd.AddMonths(-3);
                        break;
                    case "HalfYear":
                        curEnd = now;
                        curStart = now.AddMonths(-6);
                        prevEnd = curStart.AddDays(-1);
                        prevStart = prevEnd.AddMonths(-6);
                        break;
                    case "Year":
                        curEnd = now;
                        curStart = now.AddYears(-1);
                        prevEnd = curStart.AddDays(-1);
                        prevStart = prevEnd.AddYears(-1);
                        break;
                    default:
                        return;
                }

                CurrentStartDatePicker.SelectedDate = curStart;
                CurrentEndDatePicker.SelectedDate = curEnd;
                PreviousStartDatePicker.SelectedDate = prevStart;
                PreviousEndDatePicker.SelectedDate = prevEnd;
            }
        }


        // ═══════════════════════════════════════════════════════
        //  ЗАПОЛНЕНИЕ KPI-КАРТОЧЕК
        // ═══════════════════════════════════════════════════════

        private void FillKpiCards(KpiDeltas deltas, BaseReport current, BaseReport previous)
        {
            // Выручка
            RevenueCurrentText.Text = $"{current.TotalRevenue:N0} ₽";
            RevenuePreviousText.Text = $"{previous.TotalRevenue:N0} ₽";
            SetDeltaText(RevenueDeltaText, deltas.RevenueChange, deltas.RevenueChangePercent);

            // Прибыль
            ProfitCurrentText.Text = $"{current.NetProfit:N0} ₽";
            ProfitPreviousText.Text = $"{previous.NetProfit:N0} ₽";
            SetDeltaText(ProfitDeltaText, deltas.ProfitChange, deltas.ProfitChangePercent);

            // Заказы
            CarsCurrentText.Text = current.TotalCars.ToString();
            CarsPreviousText.Text = previous.TotalCars.ToString();
            SetDeltaText(CarsDeltaText, deltas.CarsChange, deltas.CarsChangePercent);

            // Средний чек
            AvgCheckCurrentText.Text = $"{current.AverageCheck:N0} ₽";
            AvgCheckPreviousText.Text = $"{previous.AverageCheck:N0} ₽";
            SetDeltaText(AvgCheckDeltaText, deltas.AvgCheckChange, deltas.AvgCheckChangePercent);

            // Расходы
            ExpensesCurrentText.Text = $"{current.TotalExpenses:N0} ₽";
            ExpensesPreviousText.Text = $"{previous.TotalExpenses:N0} ₽";
            SetDeltaText(ExpensesDeltaText, deltas.ExpensesChange, deltas.ExpensesChangePercent);

            // Новые клиенты
            NewClientsCurrentText.Text = current.NewClientsCount.ToString();
            NewClientsPreviousText.Text = previous.NewClientsCount.ToString();
            SetDeltaText(NewClientsDeltaText, deltas.NewClientsChange, deltas.NewClientsChangePercent);
        }

        private void SetDeltaText(System.Windows.Controls.TextBlock textBlock, decimal change, decimal percent)
        {
            string arrow = change > 0 ? "↑" : change < 0 ? "↓" : "→";
            textBlock.Text = $"{arrow} {percent:+0.0;-0.0;0}%";

            if (change > 0)
                textBlock.Foreground = TryFindResource("AccentGreen") as Brush ?? Brushes.Green;
            else if (change < 0)
                textBlock.Foreground = TryFindResource("AccentRed") as Brush ?? Brushes.Red;
            else
                textBlock.Foreground = TryFindResource("TextMuted") as Brush ?? Brushes.Gray;
        }

        // ═══════════════════════════════════════════════════════
        //  ЗАПОЛНЕНИЕ ДЕПАРТАМЕНТОВ
        // ═══════════════════════════════════════════════════════

        private void FillDepartments(DepartmentComparisonData data)
        {
            // Мойка
            WashRevenuePrevText.Text = $"{data.Wash.PreviousRevenue:N0} ₽";
            WashRevenueCurText.Text = $"{data.Wash.CurrentRevenue:N0} ₽";
            SetDeltaText(WashRevenueDeltaText, data.Wash.CurrentRevenue - data.Wash.PreviousRevenue, data.Wash.RevenueChangePercent);

            WashCarsPrevText.Text = data.Wash.PreviousCars.ToString();
            WashCarsCurText.Text = data.Wash.CurrentCars.ToString();
            SetDeltaText(WashCarsDeltaText, data.Wash.CurrentCars - data.Wash.PreviousCars, data.Wash.CarsChangePercent);

            WashProfitPrevText.Text = $"{data.Wash.PreviousProfit:N0} ₽";
            WashProfitCurText.Text = $"{data.Wash.CurrentProfit:N0} ₽";
            SetDeltaText(WashProfitDeltaText, data.Wash.CurrentProfit - data.Wash.PreviousProfit, data.Wash.ProfitChangePercent);

            // Сервис
            ServiceRevenuePrevText.Text = $"{data.Service.PreviousRevenue:N0} ₽";
            ServiceRevenueCurText.Text = $"{data.Service.CurrentRevenue:N0} ₽";
            SetDeltaText(ServiceRevenueDeltaText, data.Service.CurrentRevenue - data.Service.PreviousRevenue, data.Service.RevenueChangePercent);

            ServiceCarsPrevText.Text = data.Service.PreviousCars.ToString();
            ServiceCarsCurText.Text = data.Service.CurrentCars.ToString();
            SetDeltaText(ServiceCarsDeltaText, data.Service.CurrentCars - data.Service.PreviousCars, data.Service.CarsChangePercent);

            ServiceProfitPrevText.Text = $"{data.Service.PreviousProfit:N0} ₽";
            ServiceProfitCurText.Text = $"{data.Service.CurrentProfit:N0} ₽";
            SetDeltaText(ServiceProfitDeltaText, data.Service.CurrentProfit - data.Service.PreviousProfit, data.Service.ProfitChangePercent);
        }

        // ═══════════════════════════════════════════════════════
        //  ГРАФИКИ ПО ДНЯМ
        // ═══════════════════════════════════════════════════════

        private void FillDailyCharts(DailyComparisonData data)
        {
            var curDays = data.CurrentDays;
            var prevDays = data.PreviousDays;

            // Метки для оси X (по дням)
            int maxDays = Math.Max(curDays.Count, prevDays.Count);
            CurrentDayLabels = Enumerable.Range(0, maxDays)
                .Select(i => i < curDays.Count ? curDays[i].DateLabel : "")
                .ToArray();

            // Метки для оси X (по процентам)
            PercentDayLabels = Enumerable.Range(0, maxDays)
                .Select(i => $"День {i + 1}")
                .ToArray();

            var accentBlue = TryFindResource("AccentBlue") as Brush ?? new SolidColorBrush(Color.FromRgb(52, 152, 219));
            var mutedGray = TryFindResource("TextMuted") as Brush ?? new SolidColorBrush(Color.FromRgb(150, 150, 150));

            // === Выручка по дням ===
            RevenueByDaySeries = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Текущий",
                    Values = new ChartValues<decimal>(curDays.Select(d => d.Revenue)),
                    Stroke = accentBlue, Fill = Brushes.Transparent,
                    PointGeometrySize = 6, StrokeThickness = 2
                },
                new LineSeries
                {
                    Title = "Прошлый",
                    Values = new ChartValues<decimal>(prevDays.Select(d => d.Revenue)),
                    Stroke = mutedGray, Fill = Brushes.Transparent,
                    PointGeometrySize = 4, StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 5, 3 }
                }
            };

            // === Выручка по процентам ===
            RevenueByPercentSeries = BuildPercentSeries(
                curDays.Select(d => d.Revenue).ToList(),
                prevDays.Select(d => d.Revenue).ToList(),
                accentBlue, mutedGray);

            // === Заказы по дням ===
            CarsByDaySeries = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Текущий",
                    Values = new ChartValues<int>(curDays.Select(d => d.CarsCount)),
                    Stroke = accentBlue, Fill = Brushes.Transparent,
                    PointGeometrySize = 6, StrokeThickness = 2
                },
                new LineSeries
                {
                    Title = "Прошлый",
                    Values = new ChartValues<int>(prevDays.Select(d => d.CarsCount)),
                    Stroke = mutedGray, Fill = Brushes.Transparent,
                    PointGeometrySize = 4, StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 5, 3 }
                }
            };

            // === Заказы по процентам ===
            CarsByPercentSeries = BuildPercentSeries(
                curDays.Select(d => (decimal)d.CarsCount).ToList(),
                prevDays.Select(d => (decimal)d.CarsCount).ToList(),
                accentBlue, mutedGray);

            // === Средний чек по дням ===
            AvgCheckByDaySeries = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Текущий",
                    Values = new ChartValues<decimal>(curDays.Select(d => d.AvgCheck)),
                    Stroke = accentBlue, Fill = Brushes.Transparent,
                    PointGeometrySize = 6, StrokeThickness = 2
                },
                new LineSeries
                {
                    Title = "Прошлый",
                    Values = new ChartValues<decimal>(prevDays.Select(d => d.AvgCheck)),
                    Stroke = mutedGray, Fill = Brushes.Transparent,
                    PointGeometrySize = 4, StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 5, 3 }
                }
            };

            // === Средний чек по процентам ===
            AvgCheckByPercentSeries = BuildPercentSeries(
                curDays.Select(d => d.AvgCheck).ToList(),
                prevDays.Select(d => d.AvgCheck).ToList(),
                accentBlue, mutedGray);

            // Уведомляем UI
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RevenueByDaySeries)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RevenueByPercentSeries)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CarsByDaySeries)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CarsByPercentSeries)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AvgCheckByDaySeries)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AvgCheckByPercentSeries)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentDayLabels)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PercentDayLabels)));
        }

        private SeriesCollection BuildPercentSeries(
            List<decimal> currentValues,
            List<decimal> previousValues,
            Brush currentBrush, Brush previousBrush)
        {
            decimal curMax = currentValues.Any() ? currentValues.Max() : 1;
            decimal prevMax = previousValues.Any() ? previousValues.Max() : 1;

            var curPercent = currentValues.Select(v => curMax > 0 ? Math.Round(v / curMax * 100, 1) : 0).ToList();
            var prevPercent = previousValues.Select(v => prevMax > 0 ? Math.Round(v / prevMax * 100, 1) : 0).ToList();

            return new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Текущий",
                    Values = new ChartValues<decimal>(curPercent),
                    Stroke = currentBrush, Fill = Brushes.Transparent,
                    PointGeometrySize = 6, StrokeThickness = 2
                },
                new LineSeries
                {
                    Title = "Прошлый",
                    Values = new ChartValues<decimal>(prevPercent),
                    Stroke = previousBrush, Fill = Brushes.Transparent,
                    PointGeometrySize = 4, StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 5, 3 }
                }
            };
        }

        // ═══════════════════════════════════════════════════════
        //  ЗАГРУЖЕННОСТЬ ПО ЧАСАМ
        // ═══════════════════════════════════════════════════════

        private void FillHourlyLoadCharts(List<HourlyLoad> current, List<HourlyLoad> previous)
        {
            var accentBlue = TryFindResource("AccentBlue") as Brush ?? new SolidColorBrush(Color.FromRgb(52, 152, 219));
            var mutedGray = TryFindResource("TextMuted") as Brush ?? new SolidColorBrush(Color.FromRgb(150, 150, 150));

            var curValues = Enumerable.Range(0, 24)
                .Select(h => current.FirstOrDefault(x => x.Hour == h)?.OrdersCount ?? 0)
                .ToList();
            var prevValues = Enumerable.Range(0, 24)
                .Select(h => previous.FirstOrDefault(x => x.Hour == h)?.OrdersCount ?? 0)
                .ToList();

            // Абсолютные значения
            HourlyLoadSeries = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Текущий",
                    Values = new ChartValues<int>(curValues),
                    Stroke = accentBlue, Fill = Brushes.Transparent,
                    PointGeometrySize = 5, StrokeThickness = 2
                },
                new LineSeries
                {
                    Title = "Прошлый",
                    Values = new ChartValues<int>(prevValues),
                    Stroke = mutedGray, Fill = Brushes.Transparent,
                    PointGeometrySize = 3, StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 5, 3 }
                }
            };

            // Проценты
            int curMax = curValues.Any() ? curValues.Max() : 1;
            int prevMax = prevValues.Any() ? prevValues.Max() : 1;

            var curPercent = curValues.Select(v => curMax > 0 ? Math.Round((decimal)v / curMax * 100, 1) : 0).ToList();
            var prevPercent = prevValues.Select(v => prevMax > 0 ? Math.Round((decimal)v / prevMax * 100, 1) : 0).ToList();

            HourlyLoadPercentSeries = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Текущий",
                    Values = new ChartValues<decimal>(curPercent),
                    Stroke = accentBlue, Fill = Brushes.Transparent,
                    PointGeometrySize = 5, StrokeThickness = 2
                },
                new LineSeries
                {
                    Title = "Прошлый",
                    Values = new ChartValues<decimal>(prevPercent),
                    Stroke = mutedGray, Fill = Brushes.Transparent,
                    PointGeometrySize = 3, StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 5, 3 }
                }
            };

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HourlyLoadSeries)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HourlyLoadPercentSeries)));
        }

        // ═══════════════════════════════════════════════════════
        //  ТАБЛИЦЫ СРАВНЕНИЯ
        // ═══════════════════════════════════════════════════════

        private void FillServicesComparison(List<ServiceComparisonItem> items)
        {
            ServicesComparisonList.ItemsSource = items;
        }

        private void FillExpensesComparison(List<ExpenseComparisonItem> items)
        {
            ExpensesComparisonList.ItemsSource = items;
        }

        private void FillClientsComparison(ClientComparisonData data)
        {
            ClientsUniqueCurText.Text = data.CurrentUnique.ToString();
            ClientsUniquePrevText.Text = data.PreviousUnique.ToString();

            ClientsNewCurText.Text = data.CurrentNew.ToString();
            ClientsNewPrevText.Text = data.PreviousNew.ToString();

            ClientsRepeatCurText.Text = data.CurrentRepeat.ToString();
            ClientsRepeatPrevText.Text = data.PreviousRepeat.ToString();

            ClientsRetentionCurText.Text = $"{data.CurrentRetentionRate:N1}%";
            ClientsRetentionPrevText.Text = $"{data.PreviousRetentionRate:N1}%";
        }

        private void FillFotComparison(FotComparisonData data)
        {
            // Агрегаты
            FotTotalCurText.Text = $"{data.CurrentTotalFot:N0} ₽";
            FotTotalPrevText.Text = $"{data.PreviousTotalFot:N0} ₽";
            SetDeltaText(FotTotalDeltaText,
                data.CurrentTotalFot - data.PreviousTotalFot,
                data.TotalFotChangePercent);

            FotAvgCurText.Text = $"{data.CurrentAvgPerEmployee:N0} ₽";
            FotAvgPrevText.Text = $"{data.PreviousAvgPerEmployee:N0} ₽";

            FotCountCurText.Text = data.CurrentEmployeesCount.ToString();
            FotCountPrevText.Text = data.PreviousEmployeesCount.ToString();

            // Таблица сотрудников
            FotEmployeesList.ItemsSource = data.Employees;
        }

        // ═══════════════════════════════════════════════════════
        //  ЗАКРЫТИЕ ОВЕРЛЕЯ
        // ═══════════════════════════════════════════════════════

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseOverlay();
        }

        private void OverlayBackground_Click(object sender, MouseButtonEventArgs e)
        {
            // Клик по затемнённому фону закрывает оверлей
            CloseOverlay();
        }

        private void OverlayCard_Click(object sender, MouseButtonEventArgs e)
        {
            // Клик по карточке НЕ закрывает оверлей
            e.Handled = true;
        }

        private void CloseOverlay()
        {
            Closed?.Invoke();
        }

        private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CloseOverlay();
            }
        }

        // ═══════════════════════════════════════════════════════
        //  ЭКСПОРТ В EXCEL
        // ═══════════════════════════════════════════════════════

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_comparisonResult == null)
            {
                MessageBox.Show("Сначала выполните сравнение!", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                DefaultExt = "xlsx",
                FileName = $"Сравнение_Периодов_{DateTime.Now:dd.MM.yyyy}"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    ExcelExporter.ExportPeriodComparison(_comparisonResult, saveDialog.FileName);
                    MessageBox.Show($"Сравнение экспортировано!\n\nФайл: {saveDialog.FileName}",
                        "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Ошибка экспорта",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    /// <summary>
    /// Умножает значение на коэффициент из ConverterParameter (например, 0.92 = 92%)
    /// </summary>
    public class PercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            /// Если параметр не указан, используем 90%
            /// Если параметр указан, пытаемся распарсить его как double
            /// Если value не double, возвращаем его без изменений
            /// Если value double, умножаем на коэффициент и возвращаем

            // Пример использования в XAML: {Binding SomeValue, Converter={StaticResource PercentConverter}, ConverterParameter=0.92}

            /// Тут можно указать любой коэффициент, например 0.85 для 85%
            /// Коэффициент определяет насколько уменьшается или увеличивается размер оверлея относительно предыдущего окна (CustomReportWindow)
            /// Коэффициент должен быть в формате double, с точкой как разделителем десятичных.
            /// Например, 0.9 = 90%, 1.1 = 110%

            double percent = 0.9;
            if (parameter is string p)
                double.TryParse(p, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out percent);
            if (value is double v) return v * percent;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotImplementedException();
    }
}