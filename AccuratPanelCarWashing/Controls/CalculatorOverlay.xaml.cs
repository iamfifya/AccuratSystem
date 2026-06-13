using AccuratPanelCarWashing.Models;
using AccuratSystem.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AccuratPanelCarWashing.Controls
{
    public partial class CalculatorOverlay : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // --- Классический калькулятор ---
        private string _classicResult = "0";
        public string ClassicResult
        {
            get => _classicResult;
            set { _classicResult = value; OnPropertyChanged(nameof(ClassicResult)); }
        }

        private decimal _currentVal = 0;
        private decimal _prevVal = 0;
        private string _pendingOp = "";
        private bool _isNewEntry = true;

        // --- Денежный калькулятор ---
        public ObservableCollection<MoneyDenomination> Denominations { get; set; }

        public decimal MoneyTotal => Denominations.Sum(d => d.Total);

        public CalculatorOverlay()
        {
            InitializeComponent();
            DataContext = this;

            // Инициализируем купюры и монеты
            int[] values = { 5000, 2000, 1000, 500, 200, 100, 50, 10, 10, 5, 2, 1 };
            Denominations = new ObservableCollection<MoneyDenomination>();
            foreach (var v in values)
            {
                Denominations.Add(new MoneyDenomination { Value = v, Count = 0 });
            }

            // Подписываемся на изменения в каждой купюре, чтобы обновлять общий итог
            foreach (var d in Denominations)
            {
                d.PropertyChanged += (s, e) => {
                    if (e.PropertyName == nameof(MoneyDenomination.Count))
                        OnPropertyChanged(nameof(MoneyTotal));
                };
            }
        }

        #region Классический калькулятор logic
        private void Calc_Num_Click(object sender, RoutedEventArgs e)
        {
            string digit = (sender as Button).Content.ToString();
            if (_isNewEntry)
            {
                ClassicResult = digit;
                _isNewEntry = false;
            }
            else
            {
                ClassicResult += digit;
            }
        }

        private void Calc_Comma_Click(object sender, RoutedEventArgs e)
        {
            if (!ClassicResult.Contains(",")) ClassicResult += ",";
        }

        private void Calc_Op_Click(object sender, RoutedEventArgs e)
        {
            _prevVal = decimal.Parse(ClassicResult.Replace(".", ","));
            _pendingOp = (sender as Button).Content.ToString();
            _isNewEntry = true;
        }

        private void Calc_Clear_Click(object sender, RoutedEventArgs e)
        {
            ClassicResult = "0";
            _currentVal = 0;
            _prevVal = 0;
            _pendingOp = "";
            _isNewEntry = true;
        }

        private void Calc_Equal_Click(object sender, RoutedEventArgs e)
        {
            decimal current = decimal.Parse(ClassicResult.Replace(".", ","));
            decimal result = 0;

            switch (_pendingOp)
            {
                case "+": result = _prevVal + current; break;
                case "-": result = _prevVal - current; break;
                case "×": result = _prevVal * current; break;
                case "÷": result = _prevVal / current; break;
                default: result = current; break;
            }

            ClassicResult = result.ToString();
            _isNewEntry = true;
            _pendingOp = "";
        }

        private void Calc_Sign_Click(object sender, RoutedEventArgs e)
        {
            decimal val = decimal.Parse(ClassicResult.Replace(".", ","));
            ClassicResult = (-val).ToString();
        }

        private void Calc_Percent_Click(object sender, RoutedEventArgs e)
        {
            decimal val = decimal.Parse(ClassicResult.Replace(".", ","));
            ClassicResult = (val / 100).ToString();
        }
        #endregion

        #region Денежный калькулятор logic
        private void Money_Plus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MoneyDenomination denom)
            {
                denom.Count++;
            }
        }

        private void Money_Minus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MoneyDenomination denom)
            {
                if (denom.Count > 0) denom.Count--;
            }
        }
        #endregion

        private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => Hide();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

        public void Show()
        {
            this.Visibility = Visibility.Visible;
            OverlayBackground.Visibility = Visibility.Visible;
            PopupPanel.Visibility = Visibility.Visible;

            // Запускаем анимацию появления
            var sb = (System.Windows.Media.Animation.Storyboard)this.Resources["ShowAnimation"];
            sb?.Begin();
        }

        public void Hide()
        {
            var sb = (System.Windows.Media.Animation.Storyboard)this.Resources["HideAnimation"];
            if (sb != null)
            {
                // Подписываемся на завершение анимации, чтобы скрыть элемент только ПОСЛЕ затухания
                EventHandler completedHandler = null;
                completedHandler = (s, e) =>
                {
                    sb.Completed -= completedHandler;
                    this.Visibility = Visibility.Collapsed;
                    OverlayBackground.Visibility = Visibility.Collapsed;
                    PopupPanel.Visibility = Visibility.Collapsed;
                };
                sb.Completed += completedHandler;
                sb.Begin();
            }
            else
            {
                this.Visibility = Visibility.Collapsed;
                OverlayBackground.Visibility = Visibility.Collapsed;
                PopupPanel.Visibility = Visibility.Collapsed;
            }
        }


        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
