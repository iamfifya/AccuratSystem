using AccuratPanelCWM.Models;
using AccuratPanelCWM.Services;

namespace AccuratPanelCWM.Views;

public partial class ReportsPage : ContentPage
{
    private readonly ApiService _apiService;

    public ReportsPage()
    {
        InitializeComponent();
        _apiService = new ApiService();

        // По умолчанию отчет за текущий месяц
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

            // Жестко кастуем (DateTime), чтобы избежать ошибки CS1061 с nullable DateTime?
            DateTime startDate = (DateTime)StartDatePicker.Date;
            DateTime endDate = (DateTime)EndDatePicker.Date;

            DateTime start = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            DateTime end = DateTime.SpecifyKind(endDate.AddDays(1).AddSeconds(-1), DateTimeKind.Utc);

            // 1. Загружаем статистику клиентов
            var clientStats = await _apiService.GetClientsStatsAsync(branchId, start, end);
            NewClientsLabel.Text = clientStats.NewClients.ToString();

            // 2. Загружаем отчеты по сменам
            var reports = await _apiService.GetShiftReportsAsync(branchId, start, end);
            ReportsCollectionView.ItemsSource = reports;

            // 3. Считаем общие итоги (ИСПРАВЛЕНО НА ТВОИ СВОЙСТВА)
            decimal totalRevenue = reports.Sum(r => r.TotalRevenue);
            int totalCars = reports.Sum(r => r.TotalCars);

            TotalRevenueLabel.Text = $"{totalRevenue:N0} ₽";

            if (totalCars > 0)
                AverageCheckLabel.Text = $"{(totalRevenue / totalCars):N0} ₽";
            else
                AverageCheckLabel.Text = "0 ₽";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", "Не удалось загрузить отчет: " + ex.Message, "OK");
        }
    }
}