using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DaEtoZhe.Desktop
{
    public partial class CustomDatePicker : UserControl
    {
        public event EventHandler<DateTime?> SelectedDateChanged;

        private DateTime _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        private bool _isInitialized;
        private bool _suppressDateEvent;

        public ObservableCollection<CalendarDayCell> CalendarDays { get; } = new();

        public static readonly DependencyProperty SelectedDateProperty =
            DependencyProperty.Register(
                nameof(SelectedDate),
                typeof(DateTime?),
                typeof(CustomDatePicker),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedDateChanged,
                    CoerceSelectedDate));

        public static readonly DependencyProperty MinDateProperty =
            DependencyProperty.Register(
                nameof(MinDate),
                typeof(DateTime?),
                typeof(CustomDatePicker),
                new FrameworkPropertyMetadata(null, OnDateRangeChanged));

        public static readonly DependencyProperty MaxDateProperty =
            DependencyProperty.Register(
                nameof(MaxDate),
                typeof(DateTime?),
                typeof(CustomDatePicker),
                new FrameworkPropertyMetadata(null, OnDateRangeChanged));

        public static readonly DependencyProperty DateFormatProperty =
            DependencyProperty.Register(
                nameof(DateFormat),
                typeof(string),
                typeof(CustomDatePicker),
                new FrameworkPropertyMetadata("dd.MM.yyyy", OnDateFormatChanged));

        public DateTime? SelectedDate
        {
            get => (DateTime?)GetValue(SelectedDateProperty);
            set => SetValue(SelectedDateProperty, value);
        }

        public DateTime? MinDate
        {
            get => (DateTime?)GetValue(MinDateProperty);
            set => SetValue(MinDateProperty, value);
        }

        public DateTime? MaxDate
        {
            get => (DateTime?)GetValue(MaxDateProperty);
            set => SetValue(MaxDateProperty, value);
        }

        public string DateFormat
        {
            get => (string)GetValue(DateFormatProperty);
            set => SetValue(DateFormatProperty, value);
        }

        public CustomDatePicker()
        {
            InitializeComponent();
            Loaded += CustomDatePicker_Loaded;

            // The control opens on the current month but remains empty until a date is selected.
            _currentMonth = FirstDayOfMonth(DateTime.Today);
        }

        private static object CoerceSelectedDate(DependencyObject d, object baseValue)
        {
            if (!(d is CustomDatePicker picker) || !(baseValue is DateTime value))
                return baseValue;

            var date = value.Date;
            if (picker.MinDate.HasValue && date < picker.MinDate.Value.Date)
                date = picker.MinDate.Value.Date;
            if (picker.MaxDate.HasValue && date > picker.MaxDate.Value.Date)
                date = picker.MaxDate.Value.Date;

            return date;
        }

        private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is CustomDatePicker picker))
                return;

            if (e.NewValue is DateTime date)
                picker._currentMonth = FirstDayOfMonth(date);

            if (!picker._isInitialized)
                return;

            picker.UpdateText();
            picker.UpdateCalendar();

            if (!picker._suppressDateEvent)
                picker.SelectedDateChanged?.Invoke(picker, picker.SelectedDate);
        }

        private static void OnDateRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is CustomDatePicker picker))
                return;

            picker.CoerceValue(SelectedDateProperty);
            picker._currentMonth = picker.ClampMonthToRange(picker._currentMonth);
            picker.UpdateText();
            picker.UpdateCalendar();
        }

        private static void OnDateFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CustomDatePicker picker && picker._isInitialized)
                picker.UpdateText();
        }

        private void CustomDatePicker_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                _currentMonth = SelectedDate.HasValue
                    ? FirstDayOfMonth(SelectedDate.Value)
                    : ClampMonthToRange(_currentMonth);
            }

            UpdateText();
            UpdateCalendar();
        }

        private void UpdateText()
        {
            if (DateTextBox == null)
                return;

            DateTextBox.Text = SelectedDate.HasValue
                ? FormatDate(SelectedDate.Value)
                : string.Empty;
        }

        private string FormatDate(DateTime date)
        {
            var format = string.IsNullOrWhiteSpace(DateFormat) ? "dd.MM.yyyy" : DateFormat;
            try
            {
                return date.ToString(format, CultureInfo.CurrentCulture);
            }
            catch (FormatException)
            {
                return date.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture);
            }
        }

        private void UpdateCalendar()
        {
            if (!_isInitialized)
                return;

            _currentMonth = FirstDayOfMonth(_currentMonth);
            MonthYearText.Text = _currentMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);

            CalendarDays.Clear();

            var firstDay = _currentMonth;
            var dayOfWeek = (int)firstDay.DayOfWeek;
            if (dayOfWeek == 0)
                dayOfWeek = 7;

            var startDate = firstDay.AddDays(-(dayOfWeek - 1));

            for (var i = 0; i < 42; i++)
            {
                var date = startDate.AddDays(i).Date;
                var isCurrentMonth = date.Month == _currentMonth.Month && date.Year == _currentMonth.Year;
                var isToday = date == DateTime.Today;
                var isSelected = SelectedDate.HasValue && date == SelectedDate.Value.Date;
                var isEnabled = IsDateSelectable(date);

                CalendarDays.Add(new CalendarDayCell
                {
                    Date = date,
                    IsCurrentMonth = isCurrentMonth,
                    IsToday = isToday,
                    IsSelected = isSelected,
                    IsEnabled = isEnabled,
                    DisplayToolTip = date.ToString("dddd, dd MMMM yyyy", CultureInfo.CurrentCulture)
                });
            }

            PrevMonthButton.IsEnabled = MonthContainsSelectableDate(_currentMonth.AddMonths(-1));
            NextMonthButton.IsEnabled = MonthContainsSelectableDate(_currentMonth.AddMonths(1));
            TodayButton.IsEnabled = IsDateSelectable(DateTime.Today);
        }

        private void DateTextBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsEnabled || e.LeftButton != MouseButtonState.Pressed)
                return;

            OpenCalendar();
            e.Handled = true;
        }

        private void CalendarButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsEnabled)
                return;

            if (CalendarPopup.IsOpen)
                CloseCalendar();
            else
                OpenCalendar();

            e.Handled = true;
        }

        private void OpenCalendar()
        {
            _currentMonth = SelectedDate.HasValue
                ? FirstDayOfMonth(SelectedDate.Value)
                : ClampMonthToRange(_currentMonth);

            UpdateCalendar();
            CalendarPopup.IsOpen = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                FocusDateButton(SelectedDate ?? GetPreferredFocusDate());
            }), DispatcherPriority.Input);
        }

        private void CloseCalendar()
        {
            CalendarPopup.IsOpen = false;
            DateTextBox.Focus();
        }

        private void PrevMonth_Click(object sender, RoutedEventArgs e)
        {
            if (!PrevMonthButton.IsEnabled)
                return;

            _currentMonth = _currentMonth.AddMonths(-1);
            UpdateCalendar();
            FocusDateButtonDeferred(GetPreferredFocusDate());
            e.Handled = true;
        }

        private void NextMonth_Click(object sender, RoutedEventArgs e)
        {
            if (!NextMonthButton.IsEnabled)
                return;

            _currentMonth = _currentMonth.AddMonths(1);
            UpdateCalendar();
            FocusDateButtonDeferred(GetPreferredFocusDate());
            e.Handled = true;
        }

        private void Today_Click(object sender, RoutedEventArgs e)
        {
            if (!IsDateSelectable(DateTime.Today))
                return;

            _currentMonth = FirstDayOfMonth(DateTime.Today);
            SelectDate(DateTime.Today);
            e.Handled = true;
        }

        private void DayButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.DataContext is CalendarDayCell day) || !day.IsEnabled)
                return;

            SelectDate(day.Date);
            e.Handled = true;
        }

        private void SelectDate(DateTime date)
        {
            date = date.Date;
            if (!IsDateSelectable(date))
                return;

            _suppressDateEvent = false;
            SelectedDate = date;
            _currentMonth = FirstDayOfMonth(date);
            UpdateText();
            UpdateCalendar();
            CloseCalendar();
        }

        private void DateTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Space || e.Key == Key.Down)
            {
                OpenCalendar();
                e.Handled = true;
            }
        }

        private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Root handles only keys while the field itself has focus.
            if (CalendarPopup.IsOpen && e.Key == Key.Escape)
            {
                CloseCalendar();
                e.Handled = true;
            }
        }

        private void CalendarPopup_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CloseCalendar();
                e.Handled = true;
                return;
            }

            if (!(e.OriginalSource is Button button) || !(button.DataContext is CalendarDayCell day))
                return;

            DateTime? target = null;
            switch (e.Key)
            {
                case Key.Left:
                    target = day.Date.AddDays(-1);
                    break;
                case Key.Right:
                    target = day.Date.AddDays(1);
                    break;
                case Key.Up:
                    target = day.Date.AddDays(-7);
                    break;
                case Key.Down:
                    target = day.Date.AddDays(7);
                    break;
                case Key.Home:
                    target = new DateTime(day.Date.Year, day.Date.Month, 1);
                    break;
                case Key.End:
                    target = new DateTime(day.Date.Year, day.Date.Month, DateTime.DaysInMonth(day.Date.Year, day.Date.Month));
                    break;
                case Key.PageUp:
                {
                    var targetMonth = _currentMonth.AddMonths(-1);
                    if (MonthContainsSelectableDate(targetMonth))
                    {
                        _currentMonth = targetMonth;
                        UpdateCalendar();
                        FocusDateButtonDeferred(day.Date.AddMonths(-1));
                    }
                    e.Handled = true;
                    return;
                }
                case Key.PageDown:
                {
                    var targetMonth = _currentMonth.AddMonths(1);
                    if (MonthContainsSelectableDate(targetMonth))
                    {
                        _currentMonth = targetMonth;
                        UpdateCalendar();
                        FocusDateButtonDeferred(day.Date.AddMonths(1));
                    }
                    e.Handled = true;
                    return;
                }
                case Key.Enter:
                case Key.Space:
                    if (day.IsEnabled)
                        SelectDate(day.Date);
                    e.Handled = true;
                    return;
            }

            if (target.HasValue)
            {
                MoveFocusToDate(target.Value);
                e.Handled = true;
            }
        }

        private void MoveFocusToDate(DateTime date)
        {
            date = ClampDateToRange(date.Date);
            var month = FirstDayOfMonth(date);
            if (month != _currentMonth)
            {
                _currentMonth = month;
                UpdateCalendar();
            }

            FocusDateButtonDeferred(date);
        }

        private void FocusDateButtonDeferred(DateTime date)
        {
            Dispatcher.BeginInvoke(new Action(() => FocusDateButton(date)), DispatcherPriority.Input);
        }

        private void FocusDateButton(DateTime date)
        {
            for (var i = 0; i < CalendarItems.Items.Count; i++)
            {
                if (!(CalendarItems.Items[i] is CalendarDayCell cell) || cell.Date.Date != date.Date)
                    continue;

                if (CalendarItems.ItemContainerGenerator.ContainerFromIndex(i) is ContentPresenter presenter)
                {
                    var button = FindVisualChild<Button>(presenter);
                    if (button != null && button.IsEnabled)
                    {
                        button.Focus();
                        return;
                    }
                }
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                return null;

            var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                    return match;

                var nested = FindVisualChild<T>(child);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private DateTime GetPreferredFocusDate()
        {
            if (SelectedDate.HasValue && SelectedDate.Value.Month == _currentMonth.Month && SelectedDate.Value.Year == _currentMonth.Year)
                return SelectedDate.Value.Date;

            if (DateTime.Today.Month == _currentMonth.Month && DateTime.Today.Year == _currentMonth.Year && IsDateSelectable(DateTime.Today))
                return DateTime.Today;

            for (var i = 1; i <= DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month); i++)
            {
                var date = new DateTime(_currentMonth.Year, _currentMonth.Month, i);
                if (IsDateSelectable(date))
                    return date;
            }

            return _currentMonth;
        }

        private bool IsDateSelectable(DateTime date)
        {
            date = date.Date;
            if (MinDate.HasValue && date < MinDate.Value.Date)
                return false;
            if (MaxDate.HasValue && date > MaxDate.Value.Date)
                return false;
            return true;
        }

        private bool MonthContainsSelectableDate(DateTime month)
        {
            month = FirstDayOfMonth(month);
            var days = DateTime.DaysInMonth(month.Year, month.Month);
            for (var day = 1; day <= days; day++)
            {
                if (IsDateSelectable(new DateTime(month.Year, month.Month, day)))
                    return true;
            }
            return false;
        }

        private DateTime ClampMonthToRange(DateTime month)
        {
            month = FirstDayOfMonth(month);

            if (MinDate.HasValue && month < FirstDayOfMonth(MinDate.Value))
                return FirstDayOfMonth(MinDate.Value);

            if (MaxDate.HasValue && month > FirstDayOfMonth(MaxDate.Value))
                return FirstDayOfMonth(MaxDate.Value);

            return month;
        }

        private DateTime ClampDateToRange(DateTime date)
        {
            date = date.Date;
            if (MinDate.HasValue && date < MinDate.Value.Date)
                date = MinDate.Value.Date;
            if (MaxDate.HasValue && date > MaxDate.Value.Date)
                date = MaxDate.Value.Date;
            return date;
        }

        private static DateTime FirstDayOfMonth(DateTime value)
        {
            return new DateTime(value.Year, value.Month, 1);
        }

        public sealed class CalendarDayCell
        {
            public DateTime Date { get; set; }
            public bool IsCurrentMonth { get; set; }
            public bool IsToday { get; set; }
            public bool IsSelected { get; set; }
            public bool IsEnabled { get; set; }
            public string DisplayToolTip { get; set; }
        }
    }
}
