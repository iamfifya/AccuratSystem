using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace AccuratPanelCarWashing
{
    public partial class CustomReportWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private DateTime _startDate;
        private DateTime _endDate;
        private CustomPeriodReport _currentReport;
        private readonly ApiService _apiService;

        public string CurrentBranchInfo => !string.IsNullOrEmpty(AppSettings.CurrentBranchName) ? $"📍 Текущий филиал: {AppSettings.CurrentBranchName}" : "";

        public DateTime StartDate
        {
            get { return _startDate; }
            set
            {
                _startDate = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StartDate)));
            }
        }

        public DateTime EndDate
        {
            get { return _endDate; }
            set
            {
                _endDate = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EndDate)));
            }
        }

        public CustomReportWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            DataContext = this;

            StartDate = DateTime.Now.AddDays(-6);
            EndDate = DateTime.Now;

            StartDatePicker.SelectedDate = StartDate;
            EndDatePicker.SelectedDate = EndDate;
        }

        private async void GenerateReportButton_Click(object sender, RoutedEventArgs args)
        {
            try
            {
                // Берем локальное (московское) время из UI
                DateTime startDateMsk = StartDatePicker.SelectedDate ?? StartDate;
                DateTime endDateMsk = EndDatePicker.SelectedDate ?? EndDate;

                if (startDateMsk > endDateMsk)
                {
                    MessageBox.Show("Дата начала не может быть позже даты окончания", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                this.IsEnabled = false; // Блокируем UI на время тяжелого запроса

                // 1. КОНВЕРТИРУЕМ В UTC ДЛЯ ОТПРАВКИ
                DateTime startUtc = TimeHelper.ToUtc(startDateMsk.Date);
                DateTime endUtc = TimeHelper.ToUtc(endDateMsk.Date);

                // Получаем смены, статистику и транзакции ЧЕРЕЗ API
                var periodReports = await _apiService.GetShiftReportsAsync(startUtc, endUtc);
                var clientStats = await _apiService.GetClientsStatsAsync(startUtc, endUtc);
                var periodTransactions = await _apiService.GetTransactionsByDateRangeAsync(startUtc, endUtc);

                // 2. ВОЗВРАЩАЕМ В МОСКОВСКОЕ ВРЕМЯ (чтобы UI и Excel выводили правильные часы)
                foreach (var report in periodReports)
                {
                    report.Date = TimeHelper.ToMsk(report.Date);
                    report.StartTime = TimeHelper.ToMsk(report.StartTime);
                    if (report.EndTime.HasValue)
                        report.EndTime = TimeHelper.ToMsk(report.EndTime.Value);
                }

                foreach (var t in periodTransactions)
                {
                    t.DateTime = TimeHelper.ToMsk(t.DateTime);
                }

                int newClients = clientStats.NewClients;
                int uniqueClients = clientStats.UniqueClients;

                if (!periodReports.Any() && !periodTransactions.Any())
                {
                    MessageBox.Show($"За период с {startDateMsk:dd.MM.yyyy} по {endDateMsk:dd.MM.yyyy} нет данных",
                        "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Итоговые суммы по заказам
                var totalCars = periodReports.Sum(r => r.TotalCars);
                var totalRevenue = periodReports.Sum(r => r.TotalRevenue);
                var totalWasherEarnings = periodReports.Sum(r => r.TotalWasherEarnings);
                var totalCompanyEarnings = periodReports.Sum(r => r.TotalCompanyEarnings);
                var totalCashCount = periodReports.Sum(r => r.CashCount);
                var totalCashAmount = periodReports.Sum(r => r.CashAmount);
                var totalCardCount = periodReports.Sum(r => r.CardCount);
                var totalCardAmount = periodReports.Sum(r => r.CardAmount);
                var totalTransferCount = periodReports.Sum(r => r.TransferCount);
                var totalTransferAmount = periodReports.Sum(r => r.TransferAmount);
                var totalQrCount = periodReports.Sum(r => r.QrCount);
                var totalQrAmount = periodReports.Sum(r => r.QrAmount);

                // Итоговые суммы по транзакциям
                var totalExpenses = periodTransactions.Where(t => t.Type == "Расход").Sum(t => t.Amount);
                var totalAdvances = periodTransactions.Where(t => t.Type == "Аванс мойщику").Sum(t => t.Amount);

                // Дневные сводки
                var dailySummaries = periodReports
                    .OrderBy(r => r.Date)
                    .Select(r => new DailyReportSummary
                    {
                        Date = r.Date,
                        TotalCars = r.TotalCars,
                        TotalRevenue = r.TotalRevenue,
                        TotalWasherEarnings = r.TotalWasherEarnings,
                        TotalCompanyEarnings = r.TotalCompanyEarnings,
                        CashCount = r.CashCount,
                        CashAmount = r.CashAmount,
                        CardCount = r.CardCount,
                        CardAmount = r.CardAmount,
                        TransferCount = r.TransferCount,
                        TransferAmount = r.TransferAmount,
                        QrCount = r.QrCount,
                        QrAmount = r.QrAmount
                    }).ToList();

                // Статистика по сотрудникам (Зарплатная ведомость)
                var employeesData = new Dictionary<int, EmployeeReport>();

                foreach (var report in periodReports.OrderBy(r => r.Date))
                {
                    foreach (var emp in report.EmployeesWork)
                    {
                        if (!employeesData.ContainsKey(emp.EmployeeId))
                        {
                            employeesData[emp.EmployeeId] = new EmployeeReport
                            {
                                EmployeeId = emp.EmployeeId,
                                EmployeeName = emp.EmployeeName,
                                CarsWashed = 0,
                                TotalAmount = 0,
                                Earnings = 0,
                                Advances = 0,
                                DailyWork = new List<DailyEmployeeReport>()
                            };
                        }

                        employeesData[emp.EmployeeId].CarsWashed += emp.CarsWashed;
                        employeesData[emp.EmployeeId].TotalAmount += emp.TotalAmount;
                        employeesData[emp.EmployeeId].Earnings += emp.Earnings;
                    }
                }

                // Распределяем авансы по сотрудникам
                // Запрашиваем всех сотрудников с сервера ОДИН раз перед циклом
                var allUsers = await _apiService.GetUsersAsync();

                foreach (var t in periodTransactions.Where(x => x.Type == "Аванс мойщику" && x.EmployeeId.HasValue))
                {
                    int empId = t.EmployeeId.Value;
                    if (employeesData.ContainsKey(empId))
                    {
                        employeesData[empId].Advances += t.Amount;
                    }
                    else
                    {
                        // Ищем имя в скачанном списке
                        var user = allUsers.FirstOrDefault(u => u.Id == empId);
                        if (user != null)
                        {
                            employeesData[empId] = new EmployeeReport
                            {
                                EmployeeId = empId,
                                EmployeeName = user.FullName,
                                Advances = t.Amount,
                                DailyWork = new List<DailyEmployeeReport>()
                            };
                        }
                    }
                }

                var employeesReport = employeesData.Values.OrderByDescending(e => e.Earnings).ToList();
                int daysCount = (endDateMsk - startDateMsk).Days + 1;

                // Обновляем UI
                PeriodText.Text = $"Период: {startDateMsk:dd.MM.yyyy} - {endDateMsk:dd.MM.yyyy}";
                DaysCountText.Text = $"Всего дней: {daysCount}";
                TotalCarsText.Text = totalCars.ToString();
                TotalRevenueText.Text = $"{totalRevenue:N0} ₽";
                TotalWasherText.Text = $"{totalWasherEarnings:N0} ₽";
                TotalCompanyText.Text = $"{totalCompanyEarnings:N0} ₽";

                TotalAdvancesText.Text = $"{totalAdvances:N0} ₽";
                TotalExpensesText.Text = $"{totalExpenses:N0} ₽";
                NetProfitText.Text = $"{(totalCompanyEarnings - totalExpenses):N0} ₽";

                UniqueClientsText.Text = uniqueClients.ToString();
                NewClientsText.Text = newClients.ToString();

                EmployeesSalaryList.ItemsSource = employeesReport;

                ReportContent.Visibility = Visibility.Visible;
                NoDataText.Visibility = Visibility.Collapsed;

                // Сохраняем для экспорта
                _currentReport = new CustomPeriodReport
                {
                    BranchName = AppSettings.CurrentBranchName, // ← Передаем филиал
                    StartDate = startDateMsk,
                    EndDate = endDateMsk,
                    TotalCars = totalCars,
                    TotalRevenue = totalRevenue,
                    TotalWasherEarnings = totalWasherEarnings,
                    TotalCompanyEarnings = totalCompanyEarnings,
                    TotalAdvances = totalAdvances,
                    TotalExpenses = totalExpenses,
                    CashCount = totalCashCount,
                    CashAmount = totalCashAmount,
                    CardCount = totalCardCount,
                    CardAmount = totalCardAmount,
                    TransferCount = totalTransferCount,
                    TransferAmount = totalTransferAmount,
                    QrCount = totalQrCount,
                    QrAmount = totalQrAmount,
                    UniqueClientsCount = uniqueClients,
                    NewClientsCount = newClients,
                    DailyReports = dailySummaries,
                    EmployeesWork = employeesReport
                };

                CustomCashCountText.Text = totalCashCount.ToString();
                CustomCashAmountText.Text = $"{totalCashAmount:N0} ₽";
                CustomCardCountText.Text = totalCardCount.ToString();
                CustomCardAmountText.Text = $"{totalCardAmount:N0} ₽";
                CustomTransferCountText.Text = totalTransferCount.ToString();
                CustomTransferAmountText.Text = $"{totalTransferAmount:N0} ₽";
                CustomQrCountText.Text = totalQrCount.ToString();
                CustomQrAmountText.Text = $"{totalQrAmount:N0} ₽";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка формирования отчета: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                //  СНИМАЕМ БЛОКИРОВКУ ОКНА ДАЖЕ ПРИ ОШИБКАХ 
                this.IsEnabled = true;
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs args)
        {
            if (_currentReport == null)
            {
                MessageBox.Show("Сначала сформируйте отчет", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel файл (*.xlsx)|*.xlsx",
                FileName = $"Финансовый_Отчет_{_currentReport.StartDate:dd.MM.yyyy}-{_currentReport.EndDate:dd.MM.yyyy}.xlsx",
                DefaultExt = ".xlsx"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    ExcelExporter.ExportCustomPeriodReport(_currentReport, saveDialog.FileName);
                    MessageBox.Show("Отчет успешно экспортирован!", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при экспорте:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs args)
        {
            Close();
        }
    }
}
