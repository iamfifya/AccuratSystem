using AccuratPanelCWM.Models;
using AccuratPanelCWM.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace AccuratPanelCWM.Views;

public partial class ReportsPage : ContentPage
{
    private readonly ApiService _apiService;

    // Свойства для привязки графиков
    public ISeries[] RevenueSeries { get; set; }
    public ISeries[] ShareSeries { get; set; }
    public Axis[] XAxes { get; set; }

    public ReportsPage()
    {
        InitializeComponent();
        _apiService = new ApiService();

        // Устанавливаем DataContext на саму страницу, чтобы графики увидели свойства
        BindingContext = this;

        StartDatePicker.Date = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        EndDatePicker.Date = DateTime.Now;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadReportDataAsync();
    }

    private async void RefreshReport_Clicked(object sender, EventArgs e)
    {
        await LoadReportDataAsync();
    }

    private async Task LoadReportDataAsync()
    {
        try
        {
            int branchId = AppSettings.CurrentBranchId;

            DateTime startDate = (DateTime)StartDatePicker.Date;
            DateTime endDate = (DateTime)EndDatePicker.Date;

            DateTime start = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            DateTime end = DateTime.SpecifyKind(endDate.AddDays(1).AddSeconds(-1), DateTimeKind.Utc);

            var clientStats = await _apiService.GetClientsStatsAsync(branchId, start, end);
            NewClientsLabel.Text = clientStats.NewClients.ToString();

            var reports = await _apiService.GetShiftReportsAsync(branchId, start, end);
            ReportsCollectionView.ItemsSource = reports;

            decimal totalRevenue = reports.Sum(r => r.TotalRevenue);
            int totalCars = reports.Sum(r => r.TotalCars);

            TotalRevenueLabel.Text = $"{totalRevenue:N0} ₽";
            AverageCheckLabel.Text = totalCars > 0 ? $"{(totalRevenue / totalCars):N0} ₽" : "0 ₽";

            // --- ЗАПОЛНЕНИЕ ГРАФИКОВ ---
            var sortedReports = reports.OrderBy(r => r.Date).ToList();

            // 1. Линейный график выручки
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

            // 2. Круговая диаграмма долей
            ShareSeries = new ISeries[]
            {
                new PieSeries<decimal>
                {
                    Values = new decimal[] { reports.Sum(r => r.WashTotalRevenue) },
                    Name = "Мойка",
                    DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue:N0} ₽"
                },
                new PieSeries<decimal>
                {
                    Values = new decimal[] { reports.Sum(r => r.ServiceTotalRevenue) },
                    Name = "Сервис",
                    DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue:N0} ₽"
                }
            };

            // Сообщаем UI, что данные графиков обновились
            OnPropertyChanged(nameof(RevenueSeries));
            OnPropertyChanged(nameof(ShareSeries));
            OnPropertyChanged(nameof(XAxes));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", "Не удалось загрузить отчет: " + ex.Message, "OK");
        }
    }
}