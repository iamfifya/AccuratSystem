using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace AccuratPanelCWD
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
            this.Loaded += CustomDatePicker_Loaded;
            _currentDate = DateTime.Now;
            SelectedDate = DateTime.Now;
        }

        private void CustomDatePicker_Loaded(object sender, RoutedEventArgs e)
        {
            CreateCalendar();
            UpdateCalendar();
            _isInitialized = true;
            DateTextBox.Text = SelectedDate.HasValue ? SelectedDate.Value.ToString("dd.MM.yyyy") : "";
        }

        /// <summary>
        /// Безопасно получает Brush из ресурсов темы.
        /// Если ресурс не найден — возвращает fallbackBrush (чтобы не падало).
        /// </summary>
        private Brush GetThemeBrush(string key, Brush fallbackBrush)
        {
            return TryFindResource(key) as Brush ?? fallbackBrush;
        }

        private void CreateCalendar()
        {
            CalendarGrid.Children.Clear();

            for (int i = 0; i < 42; i++)
            {
                var btn = new Button
                {
                    Width = 36,
                    Height = 36,
                    Margin = new Thickness(1),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Tag = i,
                    FontSize = 13,
                    Template = CreateRoundButtonTemplate()
                };
                btn.Click += DayButton_Click;

                // Hover-эффект
                btn.MouseEnter += (s, ev) => ApplyDayHover(btn, true);
                btn.MouseLeave += (s, ev) => ApplyDayHover(btn, false);

                CalendarGrid.Children.Add(btn);
            }
        }

        private ControlTemplate CreateRoundButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border), "Border");
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6)); /// CornerRadius соответствует радиусу кнопки для поддержания общей стилистики
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));

            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(content);
            template.VisualTree = borderFactory;
            return template;
        }

        private void UpdateCalendar()
        {
            if (CalendarGrid.Children.Count != 42) return;

            DateTime firstDayOfMonth = new DateTime(_currentDate.Year, _currentDate.Month, 1);
            int firstDayOfWeek = (int)firstDayOfMonth.DayOfWeek;
            if (firstDayOfWeek == 0) firstDayOfWeek = 7;

            DateTime startDate = firstDayOfMonth.AddDays(-(firstDayOfWeek - 1));

            MonthYearText.Text = _currentDate.ToString("MMMM yyyy");

            // Кисти из темы (с фолбэками на случай проблем)
            var accentBlue = GetThemeBrush("AccentBlue", new SolidColorBrush(Color.FromRgb(52, 152, 219)));
            var bgCard = GetThemeBrush("BgCard", Brushes.White);
            var bgSelected = GetThemeBrush("BgSelected", new SolidColorBrush(Color.FromRgb(236, 240, 241)));
            var bgMain = GetThemeBrush("BgMain", new SolidColorBrush(Color.FromRgb(245, 245, 245)));
            var textMain = GetThemeBrush("TextMain", new SolidColorBrush(Color.FromRgb(44, 62, 80)));
            var textLightMuted = GetThemeBrush("TextLightMuted", new SolidColorBrush(Color.FromRgb(149, 165, 166)));

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
                    btn.Background = accentBlue;
                    btn.Foreground = bgCard; // Белый текст на синем
                    btn.FontWeight = FontWeights.Bold;
                }
                else if (isToday)
                {
                    btn.Background = bgSelected;
                    btn.Foreground = textMain;
                    btn.FontWeight = FontWeights.Bold;
                }
                else if (!isCurrentMonth)
                {
                    btn.Background = bgMain;
                    btn.Foreground = textLightMuted;
                    btn.FontWeight = FontWeights.Normal;
                }
                else
                {
                    btn.Background = bgCard;
                    btn.Foreground = textMain;
                    btn.FontWeight = FontWeights.Normal;
                }

                // Сохраняем "базовое" состояние кнопки для hover-эффекта
                btn.SetValue(TagProperty, new DayButtonState(currentDate, isSelected, isToday, isCurrentMonth));
            }
        }

        private void ApplyDayHover(Button btn, bool isHover)
        {
            if (btn.Tag is DayButtonState state)
            {
                if (state.IsSelected) return; // Выбранный день не меняем

                var accentBlue = GetThemeBrush("AccentBlue", new SolidColorBrush(Color.FromRgb(52, 152, 219)));
                var bgSelected = GetThemeBrush("BgSelected", new SolidColorBrush(Color.FromRgb(236, 240, 241)));
                var bgCard = GetThemeBrush("BgCard", Brushes.White);
                var textMain = GetThemeBrush("TextMain", new SolidColorBrush(Color.FromRgb(44, 62, 80)));

                if (isHover)
                {
                    btn.Background = state.IsToday ? accentBlue : bgSelected;
                    btn.Foreground = state.IsToday ? bgCard : textMain;
                }
                else
                {
                    if (state.IsToday)
                    {
                        btn.Background = bgSelected;
                        btn.Foreground = textMain;
                    }
                    else
                    {
                        btn.Background = bgCard;
                        btn.Foreground = textMain;
                    }
                }
            }
        }

        /// <summary>
        /// Хранит базовое состояние дня (для корректного hover-эффекта).
        /// </summary>
        private class DayButtonState
        {
            public DateTime Date { get; }
            public bool IsSelected { get; }
            public bool IsToday { get; }
            public bool IsCurrentMonth { get; }

            public DayButtonState(DateTime date, bool isSelected, bool isToday, bool isCurrentMonth)
            {
                Date = date;
                IsSelected = isSelected;
                IsToday = isToday;
                IsCurrentMonth = isCurrentMonth;
            }
        }

        private void DayButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag is DayButtonState state)
            {
                SelectedDate = state.Date;
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