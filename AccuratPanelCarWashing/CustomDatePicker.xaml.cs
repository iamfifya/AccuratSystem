using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AccuratPanelCarWashing
{
    public partial class CustomDatePicker : UserControl
    {
        public event EventHandler<DateTime?> SelectedDateChanged;
        private DateTime _currentDate;
        private bool _isUpdating;
        private bool _isInitialized;

        public static readonly DependencyProperty SelectedDateProperty =
            DependencyProperty.Register("SelectedDate", typeof(DateTime?), typeof(CustomDatePicker),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedDateChanged));

        public DateTime? SelectedDate
        {
            get => (DateTime?)GetValue(SelectedDateProperty);
            set
            {
                if (_isUpdating) return;
                _isUpdating = true;
                SetValue(SelectedDateProperty, value);
                _isUpdating = false;

                if (_isInitialized)
                {
                    DateTextBox.Text = value.HasValue ? value.Value.ToString("dd.MM.yyyy") : "";
                    UpdateCalendar();

                    // Вызываем событие
                    SelectedDateChanged?.Invoke(this, value);
                }
            }
        }


        private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var picker = d as CustomDatePicker;
            if (picker != null && !picker._isUpdating && picker._isInitialized)
            {
                picker._isUpdating = true;
                picker.DateTextBox.Text = e.NewValue != null ? ((DateTime)e.NewValue).ToString("dd.MM.yyyy") : "";
                picker.UpdateCalendar();
                picker._isUpdating = false;
            }
        }

        public CustomDatePicker()
        {
            InitializeComponent();

            // Подписываемся на событие загрузки
            this.Loaded += CustomDatePicker_Loaded;

            _currentDate = DateTime.Now;
            SelectedDate = DateTime.Now;
        }

        private void CustomDatePicker_Loaded(object sender, RoutedEventArgs e)
        {
            CreateCalendar();
            UpdateCalendar();
            _isInitialized = true;

            // Обновляем текстовое поле
            DateTextBox.Text = SelectedDate.HasValue ? SelectedDate.Value.ToString("dd.MM.yyyy") : "";
        }

        private void CreateCalendar()
        {
            // Очищаем существующие кнопки, если есть
            CalendarGrid.Children.Clear();

            for (int i = 0; i < 42; i++)
            {
                var btn = new Button
                {
                    Width = 35,
                    Height = 35,
                    Margin = new Thickness(2),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Tag = i
                };
                btn.Click += DayButton_Click;
                CalendarGrid.Children.Add(btn);
            }
        }

        private void UpdateCalendar()
        {
            // Проверяем, что кнопки уже созданы
            if (CalendarGrid.Children.Count != 42) return;

            DateTime firstDayOfMonth = new DateTime(_currentDate.Year, _currentDate.Month, 1);
            int firstDayOfWeek = (int)firstDayOfMonth.DayOfWeek;
            if (firstDayOfWeek == 0) firstDayOfWeek = 7;

            DateTime startDate = firstDayOfMonth.AddDays(-(firstDayOfWeek - 1));

            MonthYearText.Text = _currentDate.ToString("MMMM yyyy");

            for (int i = 0; i < 42; i++)
            {
                DateTime currentDate = startDate.AddDays(i);
                bool isCurrentMonth = currentDate.Month == _currentDate.Month;
                bool isToday = currentDate.Date == DateTime.Now.Date;
                bool isSelected = SelectedDate.HasValue && currentDate.Date == SelectedDate.Value.Date;

                var btn = CalendarGrid.Children[i] as Button;
                if (btn == null) continue;

                btn.Content = currentDate.Day.ToString();
                btn.Tag = currentDate;

                if (isSelected)
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(52, 152, 219));
                    btn.Foreground = new SolidColorBrush(Colors.White);
                }
                else if (isToday)
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(236, 240, 241));
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80));
                }
                else if (!isCurrentMonth)
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(245, 245, 245));
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166));
                }
                else
                {
                    btn.Background = new SolidColorBrush(Colors.White);
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80));
                }

                btn.FontWeight = (isToday && !isSelected) ? FontWeights.Bold : FontWeights.Normal;
            }
        }

        private void DayButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag is DateTime date)
            {
                SelectedDate = date;
                CalendarPopup.IsOpen = false;
            }
        }

        private void DateTextBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            CalendarPopup.IsOpen = true;
            UpdateCalendar();
        }

        private void CalendarButton_Click(object sender, RoutedEventArgs e)
        {
            CalendarPopup.IsOpen = !CalendarPopup.IsOpen;
            UpdateCalendar();
        }

        private void PrevMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentDate = _currentDate.AddMonths(-1);
            UpdateCalendar();
        }

        private void NextMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentDate = _currentDate.AddMonths(1);
            UpdateCalendar();
        }
    }
}
