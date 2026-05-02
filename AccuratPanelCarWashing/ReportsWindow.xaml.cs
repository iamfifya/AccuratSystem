using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AccuratPanelCarWashing
{
    public partial class ReportsWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private List<ShiftReport> _reports;
        private ShiftReport _selectedReport;
        private readonly ApiService _apiService;

        public List<ShiftReport> Reports
        {
            get { return _reports; }
            set
            {
                _reports = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Reports)));
            }
        }

        public ShiftReport SelectedReport
        {
            get { return _selectedReport; }
            set
            {
                _selectedReport = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedReport)));
            }
        }

        // ДОБАВЛЯЕМ СВОЙСТВО:
        public string CurrentBranchInfo => !string.IsNullOrEmpty(AppSettings.CurrentBranchName) ? $"📍 Текущий филиал: {AppSettings.CurrentBranchName}" : "";

        public ReportsWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            DataContext = this;
            LoadReports();
        }

        private async void LoadReports()
        {
            try
            {
                // Запрашиваем широкий диапазон, конвертируем границы в UTC
                DateTime startUtc = TimeHelper.ToUtc(new DateTime(2020, 1, 1));
                DateTime endUtc = TimeHelper.ToUtc(new DateTime(2050, 1, 1));

                var allReports = await _apiService.GetShiftReportsAsync(startUtc, endUtc);

                // КОНВЕРТИРУЕМ СЕРВЕРНОЕ ВРЕМЯ ОБРАТНО В МОСКОВСКОЕ
                foreach (var report in allReports)
                {
                    report.Date = TimeHelper.ToMsk(report.Date);
                    report.StartTime = TimeHelper.ToMsk(report.StartTime);
                    if (report.EndTime.HasValue)
                        report.EndTime = TimeHelper.ToMsk(report.EndTime.Value);
                }

                Reports = allReports.OrderByDescending(r => r.Date).ToList();

                if (Reports.Any())
                {
                    ReportsListBox.ItemsSource = Reports;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private void ReportSelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            SelectedReport = ReportsListBox.SelectedItem as ShiftReport;
        }

        private void CustomReportButton_Click(object sender, RoutedEventArgs e)
        {
            var customReportWin = new CustomReportWindow();
            customReportWin.ShowDialog();
        }

        private void ExportButton_Click(object sender, RoutedEventArgs args)
        {
            if (SelectedReport == null)
            {
                MessageBox.Show("Выберите отчет для экспорта", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV файлы (*.csv)|*.csv|JSON файлы (*.json)|*.json",
                    DefaultExt = "csv",
                    FileName = $"Отчет_Смена_{SelectedReport.Date:yyyy-MM-dd}"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    string extension = System.IO.Path.GetExtension(saveDialog.FileName).ToLower();

                    if (extension == ".csv")
                    {
                        ExportToCsv(SelectedReport, saveDialog.FileName);
                        MessageBox.Show($"Отчет успешно экспортирован в CSV\n\n{saveDialog.FileName}", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (extension == ".json")
                    {
                        var json = Newtonsoft.Json.JsonConvert.SerializeObject(SelectedReport, Newtonsoft.Json.Formatting.Indented);
                        System.IO.File.WriteAllText(saveDialog.FileName, json);
                        MessageBox.Show($"Отчет успешно экспортирован в JSON\n\n{saveDialog.FileName}", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToCsv(ShiftReport report, string filePath)
        {
            var lines = new List<string>();

            // ДОБАВЛЯЕМ СТРОКУ С ФИЛИАЛОМ:
            lines.Add($"Отчет по филиалу: {AppSettings.CurrentBranchName}");
            lines.Add(""); // Пустая строка для отступа

            lines.Add("Дата;Время начала;Время окончания;Машин;Выручка;Начислено Мойщикам;Расходы;Выдано авансов;Чистая прибыль(ЧПКО);Примечание");
            lines.Add($"{report.Date:dd.MM.yyyy};{report.StartTime:HH:mm};{report.EndTime:HH:mm};" +
                      $"{report.TotalCars};{report.TotalRevenue:N0};{report.TotalWasherEarnings:N0};" +
                      $"{report.TotalExpenses:N0};{report.TotalAdvances:N0};{report.NetProfit:N0};{report.Notes}");

            lines.Add("");
            lines.Add("Сотрудник;Машин;Выручка(с машин);Начислено(ЗП+Мин);Взято авансов;К ВЫПЛАТЕ");

            foreach (var emp in report.EmployeesWork)
            {
                lines.Add($"{emp.EmployeeName};{emp.CarsWashed};{emp.TotalAmount:N0};{emp.Earnings:N0};{emp.Advances:N0};{emp.ToPay:N0}");
            }

            System.IO.File.WriteAllLines(filePath, lines, System.Text.Encoding.UTF8);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs args)
        {
            Close();
        }
    }
}
