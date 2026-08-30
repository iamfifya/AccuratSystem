using AccuratPanelCWD.Models;
using AccuratSystem.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AccuratPanelCWD.Controls
{
    public partial class CalculatorOverlay : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // ДОБАВЛЕН КОНСТРУКТОР (Обязателен для WPF)
        public CalculatorOverlay()
        {
            InitializeComponent();
            DataContext = this;

            // Купюры (индексы 0–8): 5000…5 ₽ | Монеты (индексы 9–12): 10…1 ₽
            int[] values = { 5000, 2000, 1000, 500, 200, 100, 50, 10, 5, 10, 5, 2, 1 };
            foreach (var v in values)
                Denominations.Add(new MoneyDenomination { Value = v, Count = 0 });

            // Пересчёт итога при изменении любого счётчика
            foreach (var d in Denominations)
                d.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MoneyDenomination.Count))
                        OnPropertyChanged(nameof(MoneyTotal));
                };
        }
        public class MoneyDenomination : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            public int Value { get; set; }
            public string DisplayName => $"{Value} ₽";

            private int _count;
            public int Count
            {
                get => _count;
                set
                {
                    _count = value;
                    OnPropertyChanged(nameof(Count));
                    OnPropertyChanged(nameof(Total));
                }
            }

            public decimal Total => Value * Count;

            protected void OnPropertyChanged(string name) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // --- Денежный калькулятор ---
        public ObservableCollection<MoneyDenomination> Denominations { get; } = new ObservableCollection<MoneyDenomination>();

        public decimal MoneyTotal => Denominations.Sum(d => d.Total);

        // --- Классический калькулятор ---
        private string _classicResult = "0";
        public string ClassicResult
        {
            get => _classicResult;
            set { _classicResult = value; OnPropertyChanged(nameof(ClassicResult)); }
        }

        private string _classicExpression = "";
        public string ClassicExpression
        {
            get => _classicExpression;
            set { _classicExpression = value; OnPropertyChanged(nameof(ClassicExpression)); }
        }

        private decimal _prevVal = 0;
        private string _pendingOp = "";
        private bool _isNewEntry = true;
        private bool _isError = false;

        #region Классический калькулятор logic

        private void Calc_Num_Click(object sender, RoutedEventArgs e)
        {
            if (_isError) Calc_Clear_Click(null, null);

            string digit = (sender as Button).Content.ToString();

            // Ограничение на длину ввода, как в смартфонах
            if (!_isNewEntry && ClassicResult.Replace(" ", "").Length >= 15) return;

            if (_isNewEntry || ClassicResult == "0")
            {
                ClassicResult = digit;
                _isNewEntry = false;
            }
            else
            {
                // Убираем пробелы форматирования перед добавлением цифры
                string raw = ClassicResult.Replace(" ", "") + digit;
                ClassicResult = FormatNumber(raw);
            }
        }

        private void Calc_Comma_Click(object sender, RoutedEventArgs e)
        {
            if (_isError) Calc_Clear_Click(null, null);
            if (_isNewEntry)
            {
                ClassicResult = "0,";
                _isNewEntry = false;
            }
            else if (!ClassicResult.Contains(","))
            {
                ClassicResult += ",";
            }
        }

        private void Calc_Op_Click(object sender, RoutedEventArgs e)
        {
            if (_isError) return;

            string op = (sender as Button).Content.ToString();
            decimal currentVal = ParseDisplay(ClassicResult);

            // Если оператор уже был введён и мы вводим новое число, вычисляем промежуточный итог
            if (!_isNewEntry && !string.IsNullOrEmpty(_pendingOp))
            {
                CalculateResult(currentVal);
                currentVal = ParseDisplay(ClassicResult); // Берем новый результат
            }

            _prevVal = currentVal;
            _pendingOp = op;
            _isNewEntry = true;
            ClassicExpression = $"{FormatNumber(currentVal.ToString())} {op}";
        }

        private void Calc_Equal_Click(object sender, RoutedEventArgs e)
        {
            if (_isError || string.IsNullOrEmpty(_pendingOp)) return;

            decimal currentVal = ParseDisplay(ClassicResult);
            ClassicExpression = $"{FormatNumber(_prevVal.ToString())} {_pendingOp} {FormatNumber(currentVal.ToString())} =";

            CalculateResult(currentVal);

            _pendingOp = "";
            _isNewEntry = true;
        }

        private void CalculateResult(decimal current)
        {
            decimal result = 0;
            try
            {
                switch (_pendingOp)
                {
                    case "+": result = _prevVal + current; break;
                    case "-": result = _prevVal - current; break;
                    case "×": result = _prevVal * current; break;
                    case "÷":
                        if (current == 0) throw new DivideByZeroException();
                        result = _prevVal / current;
                        break;
                    default: result = current; break;
                }
                ClassicResult = FormatNumber(result.ToString("G29"));
            }
            catch (DivideByZeroException)
            {
                ClassicResult = "Ошибка";
                _isError = true;
            }
        }

        private void Calc_Sign_Click(object sender, RoutedEventArgs e)
        {
            if (_isError || ClassicResult == "0") return;

            decimal val = ParseDisplay(ClassicResult);
            ClassicResult = FormatNumber((-val).ToString("G29"));
        }

        private void Calc_Percent_Click(object sender, RoutedEventArgs e)
        {
            if (_isError) return;

            decimal current = ParseDisplay(ClassicResult);
            decimal result = 0;

            // Умные проценты: если прибавляем к числу, процент считается от предыдущего (напр. 200 + 10%)
            if (!string.IsNullOrEmpty(_pendingOp) && (_pendingOp == "+" || _pendingOp == "-"))
            {
                result = _prevVal * (current / 100m);
            }
            else
            {
                result = current / 100m;
            }

            ClassicResult = FormatNumber(result.ToString("G29"));
            // Выражение обновляется, чтобы показать, что применился процент
            ClassicExpression = $"{FormatNumber(_prevVal.ToString())} {_pendingOp} {FormatNumber(result.ToString("G29"))}";
        }

        private void Calc_Backspace_Click(object sender, RoutedEventArgs e)
        {
            if (_isError) { Calc_Clear_Click(null, null); return; }
            if (_isNewEntry) return;

            string raw = ClassicResult.Replace(" ", "");
            if (raw.Length > 1)
            {
                raw = raw.Substring(0, raw.Length - 1);
                // Если удалили всё до минуса
                if (raw == "-" || raw == "-0") raw = "0";
                ClassicResult = FormatNumber(raw);
            }
            else
            {
                ClassicResult = "0";
                _isNewEntry = true;
            }
        }

        private void Calc_Clear_Click(object sender, RoutedEventArgs e)
        {
            ClassicResult = "0";
            ClassicExpression = "";
            _prevVal = 0;
            _pendingOp = "";
            _isNewEntry = true;
            _isError = false;
        }

        // Вспомогательный метод для парсинга с учетом запятой
        private decimal ParseDisplay(string display)
        {
            string clean = display.Replace(" ", "").Replace(".", ",");
            return decimal.TryParse(clean, out decimal val) ? val : 0;
        }

        // Вспомогательный метод для красивого разделения на разряды (1 000 000)
        private string FormatNumber(string input)
        {
            input = input.Replace(" ", "");
            if (input.EndsWith(",")) return input; // Не форматируем, если пользователь только ввел запятую

            if (decimal.TryParse(input.Replace(".", ","), out decimal val))
            {
                // Учитываем наличие дробной части, чтобы не терять нули (например, 0,05)
                if (input.Contains(","))
                {
                    string[] parts = input.Split(',');
                    decimal intPart = decimal.Parse(parts[0]);
                    return $"{intPart:#,0} {parts[1]}".Trim().Replace(",", " "); // Кастомная сборка с разрядами
                }
                return val.ToString("#,0").Replace("\u00A0", " ").Replace(",", " ");
            }
            return input;
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