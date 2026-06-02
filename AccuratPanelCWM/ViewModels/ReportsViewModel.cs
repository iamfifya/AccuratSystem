using AccuratPanelCWM.Services;
using AccuratSystem.Contracts.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Maui.Storage;
using SkiaSharp;
using System.Collections.ObjectModel;

namespace AccuratPanelCWM.ViewModels
{
    public partial class ReportsViewModel : ObservableObject
    {
        private readonly ApiService _apiService;

        [ObservableProperty] private bool _isBusy;

        [ObservableProperty] private DateTime _startDate;
        [ObservableProperty] private DateTime _endDate;

        // Показатели
        [ObservableProperty] private string _totalRevenueDisplay = "0 ₽";
        [ObservableProperty] private string _newClientsDisplay = "0";
        [ObservableProperty] private string _averageCheckDisplay = "0 ₽";

        // Графики LiveCharts
        [ObservableProperty] private ISeries[] _revenueSeries;
        [ObservableProperty] private ISeries[] _shareSeries;
        [ObservableProperty] private Axis[] _xAxes;

        public ObservableCollection<ShiftReport> Reports { get; } = new();

        public ReportsViewModel(ApiService apiService)
        {
            _apiService = apiService;

            // По умолчанию выбираем текущий месяц
            var now = DateTime.Now;
            StartDate = new DateTime(now.Year, now.Month, 1);
            EndDate = now;
        }

        [RelayCommand]
        public async Task LoadReportAsync()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                int branchId = Preferences.Default.Get("CurrentBranchId", 0);
                if (branchId == 0) return;

                DateTime startUtc = DateTime.SpecifyKind(StartDate, DateTimeKind.Utc);
                DateTime endUtc = DateTime.SpecifyKind(EndDate.AddDays(1).AddSeconds(-1), DateTimeKind.Utc);

                // 💥 ОПТИМИЗАЦИЯ: Запускаем два запроса ПАРАЛЛЕЛЬНО!
                var clientStatsTask = _apiService.GetClientsStatsAsync(branchId, startUtc, endUtc);
                var reportsTask = _apiService.GetShiftReportsAsync(branchId, startUtc, endUtc);

                await Task.WhenAll(clientStatsTask, reportsTask);

                var clientStats = clientStatsTask.Result;
                var shiftReports = reportsTask.Result;

                NewClientsDisplay = clientStats.NewClients.ToString();

                // Обновляем список смен (сначала новые)
                Reports.Clear();
                foreach (var r in shiftReports.OrderByDescending(x => x.Date))
                {
                    Reports.Add(r);
                }

                decimal totalRev = shiftReports.Sum(r => r.TotalRevenue);
                int totalCars = shiftReports.Sum(r => r.TotalCars);

                TotalRevenueDisplay = $"{totalRev:N0} ₽";
                AverageCheckDisplay = totalCars > 0 ? $"{(totalRev / totalCars):N0} ₽" : "0 ₽";

                // --- ПОДГОТОВКА ГРАФИКОВ ---
                var sortedReports = shiftReports.OrderBy(r => r.Date).ToList(); // Для графиков нужна хронология

                RevenueSeries = new ISeries[]
                {
                    new LineSeries<decimal>
                    {
                        Values = sortedReports.Select(r => r.TotalRevenue).ToArray(),
                        Name = "Выручка",
                        Stroke = new SolidColorPaint(SKColors.MediumSeaGreen) { StrokeThickness = 3 },
                        GeometrySize = 10,
                        Fill = null
                    }
                };

                XAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = sortedReports.Select(r => r.Date.ToString("dd.MM")).ToArray(),
                        LabelsRotation = 45
                    }
                };

                ShareSeries = new ISeries[]
                {
                    new PieSeries<decimal>
                    {
                        Values = new decimal[] { shiftReports.Sum(r => r.WashTotalRevenue) },
                        Name = "Мойка",
                        DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue:N0} ₽"
                    },
                    new PieSeries<decimal>
                    {
                        Values = new decimal[] { shiftReports.Sum(r => r.ServiceTotalRevenue) },
                        Name = "Сервис",
                        DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue:N0} ₽"
                    }
                };
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", "Не удалось загрузить отчет: " + ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}