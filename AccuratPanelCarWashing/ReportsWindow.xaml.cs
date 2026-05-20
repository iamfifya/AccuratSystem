// === ЯВНЫЕ АЛИАСЫ ДЛЯ РАЗРЕШЕНИЯ КОНФЛИКТОВ ИМЁН ===
// UI-модель пользователя (с IsAdmin, DisplayString) — используем в окне
using WpfUser = AccuratPanelCarWashing.Models.User;
// Контрактные модели из API — используем для данных с сервера
using ContractsShiftReport = AccuratSystem.Contracts.Models.ShiftReport;
using ContractsBranch = AccuratSystem.Contracts.Models.Branch;

// Остальные using без конфликтов
using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AccuratPanelCarWashing
{
    public partial class ReportsWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private List<ContractsShiftReport> _reports;
        private ContractsShiftReport _selectedReport;
        private readonly ApiService _apiService;
        private readonly WpfUser _currentUser;

        public bool IsDirector => _currentUser?.Role == 1;

        public List<ContractsShiftReport> Reports
        {
            get { return _reports; }
            set { _reports = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Reports))); }
        }

        public ContractsShiftReport SelectedReport
        {
            get { return _selectedReport; }
            set { _selectedReport = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedReport))); }
        }

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

                // При переключении вкладки загружаем отчеты для выбранного филиала
                if (_selectedBranchTab != null)
                {
                    LoadReports(_selectedBranchTab.BranchId);
                }
            }
        }

        public ReportsWindow(WpfUser currentUser)
        {
            InitializeComponent();
            _apiService = new ApiService();
            _currentUser = currentUser;
            DataContext = this;

            InitializeTabsAsync();
        }

        private async void InitializeTabsAsync()
        {
            try
            {
                var allBranches = await _apiService.GetBranchesAsync();
                BranchTabs.Clear();

                if (IsDirector)
                {
                    //  ДОБАВЛЯЕМ ПУНКТ "ВСЯ СЕТЬ"
                    BranchTabs.Add(new BranchTabItem { BranchId = 0, BranchName = "🌐 Все филиалы (Сеть)" });

                    foreach (var b in allBranches)
                    {
                        BranchTabs.Add(new BranchTabItem { BranchId = b.Id, BranchName = b.Name });
                    }
                }
                else
                {
                    var myBranch = allBranches.FirstOrDefault(b => b.Id == AppSettings.CurrentBranchId);
                    if (myBranch != null)
                    {
                        BranchTabs.Add(new BranchTabItem { BranchId = myBranch.Id, BranchName = myBranch.Name });
                    }
                }

                if (BranchTabs.Any())
                    SelectedBranchTab = BranchTabs.First(); // Это автоматически вызовет LoadReports
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки филиалов: {ex.Message}");
            }
        }

        private async void LoadReports(int branchId)
        {
            try
            {
                DateTime startUtc = TimeHelper.ToUtc(new DateTime(2020, 1, 1));
                DateTime endUtc = TimeHelper.ToUtc(new DateTime(2050, 1, 1));

                //  Передаем branchId в API
                var allReports = await _apiService.GetShiftReportsAsync(branchId, startUtc, endUtc);

                foreach (var report in allReports)
                {
                    report.Date = TimeHelper.ToMsk(report.Date);
                    report.StartTime = TimeHelper.ToMsk(report.StartTime);
                    if (report.EndTime.HasValue)
                        report.EndTime = TimeHelper.ToMsk(report.EndTime.Value);
                }

                Reports = allReports.OrderByDescending(r => r.Date).ToList();

                ReportsListBox.ItemsSource = null; // Сброс привязки для обновления UI
                if (Reports.Any())
                {
                    ReportsListBox.ItemsSource = Reports;
                    // Автоматически выбираем первый отчет в списке
                    ReportsListBox.SelectedIndex = 0;
                }
                else
                {
                    SelectedReport = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки отчетов: {ex.Message}");
            }
        }

        private void ReportSelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            SelectedReport = ReportsListBox.SelectedItem as ContractsShiftReport;
        }

        private void CustomReportButton_Click(object sender, RoutedEventArgs e)
        {
            // Передадим пользователя и сюда
            var customReportWin = new CustomReportWindow(_currentUser);
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

        private void ExportToCsv(ContractsShiftReport report, string filePath)
        {
            var lines = new List<string>();

            string branchTitle = SelectedBranchTab != null ? SelectedBranchTab.BranchName : "Неизвестно";
            lines.Add($"Отчет по филиалу: {branchTitle}");
            lines.Add("");

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