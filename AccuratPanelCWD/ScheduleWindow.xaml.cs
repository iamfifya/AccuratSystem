using AccuratPanelCWD.Models;
using AccuratPanelCWD.Services;
using AccuratSystem.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EmployeeSchedule = AccuratSystem.Contracts.Models.EmployeeSchedule;
using WpfUser = AccuratPanelCWD.Models.User;

namespace AccuratPanelCWD
{
    public partial class ScheduleWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly ApiService _apiService;
        private readonly WpfUser _currentUser;
        private DateTime _currentDate;
        private List<EmployeeSchedule> _scheduleData;
        private Dictionary<int, Border> _dayHeaders = new Dictionary<int, Border>();
        private Dictionary<string, Border> _cells = new Dictionary<string, Border>();
        private bool _isDataModified = false;

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
                if (_selectedBranchTab != null)
                {
                    _ = LoadScheduleAsync();
                }
            }
        }

        public bool IsDirector => UserPermissions.IsSuperUser(_currentUser);

        public ScheduleWindow(WpfUser user)
        {
            InitializeComponent();
            _apiService = new ApiService();
            _currentUser = user;
            _currentDate = DateTime.Now;
            DataContext = this;

            _ = InitializeTabsAsync();
        }

        /// <summary>
        /// Безопасно получает Brush из ресурсов темы с фолбэком.
        /// </summary>
        private Brush GetThemeBrush(string key, Brush fallback = null)
        {
            return TryFindResource(key) as Brush ?? fallback ?? Brushes.Transparent;
        }

        private async Task InitializeTabsAsync()
        {
            try
            {
                var branches = await _apiService.GetBranchesAsync();
                BranchTabs.Clear();

                foreach (var b in branches)
                    BranchTabs.Add(new BranchTabItem { BranchId = b.Id, BranchName = b.Name });

                int defaultId = _currentUser?.BranchId ?? AppSettings.CurrentBranchId;
                SelectedBranchTab = BranchTabs.FirstOrDefault(t => t.BranchId == defaultId) ?? BranchTabs.FirstOrDefault();
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка загрузки филиалов: {ex.Message}"); }
        }

        private async Task LoadScheduleAsync()
        {
            if (SelectedBranchTab == null) return;

            this.IsEnabled = false;
            int branchId = SelectedBranchTab.BranchId;

            _scheduleData = await _apiService.GetScheduleAsync(branchId, _currentDate.Year, _currentDate.Month);

            if (_scheduleData == null || !_scheduleData.Any())
            {
                _scheduleData = new List<EmployeeSchedule>();
            }

            MonthYearText.Text = _currentDate.ToString("MMMM yyyy");
            BuildScheduleTable();
            _isDataModified = false;
            UpdateSaveButtonState();
            this.IsEnabled = true;
        }

        private void UpdateSaveButtonState() => SaveButton.IsEnabled = _isDataModified && _scheduleData.Any();

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_scheduleData.Any()) return;
            if (SelectedBranchTab == null) return;

            this.IsEnabled = false;
            try
            {
                await _apiService.SaveScheduleAsync(SelectedBranchTab.BranchId, _currentDate.Year, _currentDate.Month, _scheduleData);
                _isDataModified = false;
                UpdateSaveButtonState();
                MessageBox.Show($"График на {_currentDate:MMMM yyyy} сохранен", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка");
            }
            finally { this.IsEnabled = true; }
        }

        private async void TemplateButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedBranchTab == null) return;

            var result = MessageBox.Show("Создать шаблон графика?\nТекущий график будет заменен новым.", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                this.IsEnabled = false;
                await CreateDefaultScheduleAsync();
                BuildScheduleTable();
                _isDataModified = true;
                UpdateSaveButtonState();
                this.IsEnabled = true;
                MessageBox.Show("Шаблон графика создан.", "Успешно");
            }
        }

        private void PrevMonth_Click(object sender, RoutedEventArgs e)
        {
            if (_isDataModified && MessageBox.Show("У вас есть несохраненные изменения.\nПерейти к другому месяцу без сохранения?", "Предупреждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            _currentDate = _currentDate.AddMonths(-1);
            _ = LoadScheduleAsync();
        }

        private void NextMonth_Click(object sender, RoutedEventArgs e)
        {
            if (_isDataModified && MessageBox.Show("У вас есть несохраненные изменения.\nПерейти к другому месяцу без сохранения?", "Предупреждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            _currentDate = _currentDate.AddMonths(1);
            _ = LoadScheduleAsync();
        }

        private async Task CreateDefaultScheduleAsync()
        {
            int branchId = SelectedBranchTab.BranchId;
            var allEmployees = await _apiService.GetUsersAsync();

            var employees = allEmployees
                .Where(u => u.BranchId == branchId || u.RoleId == 1 || u.RoleId == 2)
                .ToList();

            if (!employees.Any())
            {
                MessageBox.Show("В этом филиале нет привязанных сотрудников!", "Ошибка");
                return;
            }

            var daysInMonth = DateTime.DaysInMonth(_currentDate.Year, _currentDate.Month);
            var prevMonthDate = _currentDate.AddMonths(-1);
            var prevMonthSchedule = await _apiService.GetScheduleAsync(branchId, prevMonthDate.Year, prevMonthDate.Month);

            var admins = employees.Where(e => e.RoleId == 1 || e.RoleId == 2).OrderBy(e => e.Id).ToList();
            var workers = employees.Where(e => e.RoleId == 3 || e.RoleId == 4).OrderBy(e => e.Id).ToList();

            _scheduleData = new List<EmployeeSchedule>();

            // АДМИНЫ
            for (int i = 0; i < admins.Count; i++)
            {
                var admin = admins[i];
                var empSchedule = new EmployeeSchedule
                {
                    EmployeeId = admin.Id,
                    EmployeeName = admin.FullName,
                    Position = "Администратор",
                    Days = new Dictionary<int, string>()
                };

                int phaseShift = i * 2;
                var prevSchedule = prevMonthSchedule?.FirstOrDefault(s => s.EmployeeId == admin.Id);
                int carryOver = 0;

                if (prevSchedule != null && prevSchedule.Days.Any())
                {
                    var lastDayPrev = DateTime.DaysInMonth(prevMonthDate.Year, prevMonthDate.Month);
                    for (int d = lastDayPrev; d >= 1; d--)
                    {
                        if (prevSchedule.Days.ContainsKey(d) && !string.IsNullOrEmpty(prevSchedule.Days[d]))
                        {
                            string last = prevSchedule.Days[d].ToUpper();
                            if (last == "Р") carryOver = 0;
                            else if (last == "В") carryOver = 2;
                            break;
                        }
                    }
                }

                for (int day = 1; day <= daysInMonth; day++)
                {
                    int cyclePos = (day + phaseShift + carryOver) % 4;
                    empSchedule.Days[day] = (cyclePos <= 1) ? "Р" : "В";
                }
                _scheduleData.Add(empSchedule);
            }

            // МОЙЩИКИ
            for (int i = 0; i < workers.Count; i++)
            {
                var worker = workers[i];
                var empSchedule = new EmployeeSchedule
                {
                    EmployeeId = worker.Id,
                    EmployeeName = worker.FullName,
                    Position = "Мойщик",
                    Days = new Dictionary<int, string>()
                };

                int shift = i % 6;
                var prevSchedule = prevMonthSchedule?.FirstOrDefault(s => s.EmployeeId == worker.Id);
                int prevOffset = 0;

                if (prevSchedule != null && prevSchedule.Days.Any())
                {
                    var lastDayPrevMonth = DateTime.DaysInMonth(prevMonthDate.Year, prevMonthDate.Month);
                    int lastActualDay = lastDayPrevMonth;
                    while (lastActualDay > 0 && !prevSchedule.Days.ContainsKey(lastActualDay)) lastActualDay--;

                    if (lastActualDay > 0 && prevSchedule.Days[lastActualDay] == "р") prevOffset = 1;
                }

                int totalShift = (shift + prevOffset) % 6;

                for (int day = 1; day <= daysInMonth; day++)
                {
                    int cyclePosition = (day + totalShift) % 6;
                    empSchedule.Days[day] = (cyclePosition >= 0 && cyclePosition <= 2) ? "р" : "в";
                }
                _scheduleData.Add(empSchedule);
            }
        }

        private void BuildScheduleTable()
        {
            ScheduleGrid.Children.Clear();
            ScheduleGrid.RowDefinitions.Clear();

            while (ScheduleGrid.ColumnDefinitions.Count > 1) ScheduleGrid.ColumnDefinitions.RemoveAt(1);
            while (HeaderGrid.ColumnDefinitions.Count > 1) { HeaderGrid.Children.RemoveAt(HeaderGrid.Children.Count - 1); HeaderGrid.ColumnDefinitions.RemoveAt(1); }

            int daysInMonth = DateTime.DaysInMonth(_currentDate.Year, _currentDate.Month);
            _dayHeaders.Clear();
            _cells.Clear();

            var gridBorderBrush = GetThemeBrush("ScheduleGridBorder", Brushes.LightGray);
            var weekendTextColor = GetThemeBrush("ScheduleWeekendText", new SolidColorBrush(Color.FromRgb(231, 76, 60)));
            var cellTextColor = GetThemeBrush("ScheduleCellText", Brushes.DarkSlateGray);

            for (int day = 1; day <= daysInMonth; day++)
            {
                HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                ScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                Border dayBorder = new Border { BorderBrush = gridBorderBrush, BorderThickness = new Thickness(1, 0, 0, 0), Background = Brushes.Transparent };
                DateTime date = new DateTime(_currentDate.Year, _currentDate.Month, day);
                bool isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

                TextBlock dayText = new TextBlock
                {
                    Text = day.ToString(),
                    Style = (Style)FindResource("DayHeaderCellStyle"),
                    Foreground = isWeekend ? weekendTextColor : Brushes.White
                };

                dayBorder.Child = dayText;
                Grid.SetColumn(dayBorder, day);
                HeaderGrid.Children.Add(dayBorder);
                _dayHeaders[day] = dayBorder;
            }

            for (int i = 0; i < _scheduleData.Count; i++)
            {
                ScheduleGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45) });
                var emp = _scheduleData[i];

                Border nameBorder = new Border { BorderBrush = gridBorderBrush, BorderThickness = new Thickness(0, 0, 1, 1), Background = GetThemeBrush("BgCard", Brushes.White) };
                TextBlock nameText = new TextBlock
                {
                    Text = emp.EmployeeName,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = GetThemeBrush("TextMain", Brushes.Black),
                    FontSize = 13
                };
                nameBorder.Child = nameText;
                Grid.SetRow(nameBorder, i);
                Grid.SetColumn(nameBorder, 0);
                ScheduleGrid.Children.Add(nameBorder);

                for (int day = 1; day <= daysInMonth; day++)
                {
                    string cellKey = $"{emp.EmployeeId}_{day}";
                    string dayValue = emp.Days.ContainsKey(day) ? emp.Days[day] : "";

                    Border cellBorder = new Border
                    {
                        BorderBrush = gridBorderBrush,
                        BorderThickness = new Thickness(0, 0, 1, 1),
                        Background = GetColorForShift(dayValue),
                        Cursor = Cursors.Hand,
                        Tag = cellKey
                    };
                    TextBlock cellText = new TextBlock
                    {
                        Text = dayValue,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontWeight = FontWeights.Bold,
                        Foreground = cellTextColor,
                        FontSize = 14
                    };

                    cellBorder.Child = cellText;
                    cellBorder.MouseLeftButtonDown += Cell_MouseLeftButtonDown;
                    cellBorder.MouseRightButtonDown += Cell_MouseRightButtonDown;

                    Grid.SetRow(cellBorder, i);
                    Grid.SetColumn(cellBorder, day);
                    ScheduleGrid.Children.Add(cellBorder);

                    _cells[cellKey] = cellBorder;
                }
            }
        }

        private Brush GetColorForShift(string shiftType)
        {
            switch (shiftType?.ToUpper())
            {
                case "Р": return GetThemeBrush("ScheduleWorkDay", new SolidColorBrush(Color.FromRgb(169, 223, 191)));
                case "В": return GetThemeBrush("ScheduleDayOff", new SolidColorBrush(Color.FromRgb(250, 215, 161)));
                case "П":
                    var skipColor = GetThemeBrush("ScheduleSkip", new SolidColorBrush(Color.FromRgb(231, 76, 60)));
                    // Делаем полупрозрачным для пропуска
                    if (skipColor is SolidColorBrush solidBrush)
                    {
                        return new SolidColorBrush(Color.FromArgb(60, solidBrush.Color.R, solidBrush.Color.G, solidBrush.Color.B));
                    }
                    return skipColor;
                default: return GetThemeBrush("BgCard", Brushes.White);
            }
        }

        private void Cell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag != null)
            {
                string key = border.Tag.ToString();
                var parts = key.Split('_');
                int empId = int.Parse(parts[0]);
                int day = int.Parse(parts[1]);

                var emp = _scheduleData.First(s => s.EmployeeId == empId);
                string current = emp.Days.ContainsKey(day) ? emp.Days[day].ToUpper() : "";

                string next;
                if (string.IsNullOrEmpty(current)) next = "Р";
                else if (current == "Р") next = "В";
                else if (current == "В") next = "П";
                else next = "";

                emp.Days[day] = next;

                if (border.Child is TextBlock textBlock) textBlock.Text = next;
                border.Background = GetColorForShift(next);

                if (!_isDataModified)
                {
                    _isDataModified = true;
                    UpdateSaveButtonState();
                }
            }
        }

        private void Cell_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag != null)
            {
                string key = border.Tag.ToString();
                var parts = key.Split('_');
                int empId = int.Parse(parts[0]);
                int day = int.Parse(parts[1]);

                var emp = _scheduleData.First(s => s.EmployeeId == empId);
                emp.Days[day] = "";

                if (border.Child is TextBlock textBlock) textBlock.Text = "";
                border.Background = GetColorForShift("");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}