using AccuratPanelCWD.Services;
using AccuratSystem.Contracts.DTOs;
using AccuratSystem.Contracts.Models;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AccuratPanelCWD
{
    public partial class XReportWindow : Window
    {
        private readonly ApiService _apiService = new ApiService();
        private readonly Shift _shift;
        private readonly decimal _expectedCash;

        public XReportWindow(Shift shift, decimal expectedCash)
        {
            InitializeComponent();
            _shift = shift;
            _expectedCash = expectedCash;

            ShiftInfoText.Text = $"Смена {shift.Date:dd.MM.yyyy} • Пересчитывает: {App.CurrentUser?.FullName ?? "неизвестно"}";
            ExpectedCashText.Text = $"{_expectedCash:N0} ₽";

            Loaded += async (s, e) => await ShowLastReconciliationAsync();
        }

        // Показываем итог ПРЕДЫДУЩЕГО пересчёта, если он был
        private async System.Threading.Tasks.Task ShowLastReconciliationAsync()
        {
            var list = await _apiService.GetReconciliationsAsync(_shift.Id);
            DifferenceHintText.Text = list.Any()
                ? $"Прошлый пересчёт: {list.First().CountedAt:dd.MM HH:mm}, {list.First().CountedBy}, разница {list.First().Difference:+0;-0;0} ₽"
                : "Пересчётов по этой смене ещё не было";
        }

        // Живая подсветка разницы при вводе
        private void ActualCashTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!decimal.TryParse(ActualCashTextBox.Text.Replace(".", ","),
                    NumberStyles.Any, CultureInfo.CurrentCulture, out decimal actual))
            {
                DifferenceText.Text = "Введите фактическую сумму";
                DifferenceText.Foreground = (Brush)FindResource("TextMuted");
                return;
            }

            var diff = actual - _expectedCash;

            if (diff == 0)
            {
                DifferenceText.Text = $"✅ Касса сходится: {actual:N0} ₽";
                DifferenceText.Foreground = (Brush)FindResource("AccentGreen");
            }
            else if (diff < 0)
            {
                DifferenceText.Text = $"🔻 Недостача: {Math.Abs(diff):N0} ₽";
                DifferenceText.Foreground = (Brush)FindResource("AccentRed");
            }
            else
            {
                DifferenceText.Text = $"🔺 Излишек: {diff:N0} ₽";
                DifferenceText.Foreground = (Brush)FindResource("AccentRed");
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(ActualCashTextBox.Text.Replace(".", ","), out decimal actual) || actual < 0)
            {
                MessageBox.Show("Введите корректную фактическую сумму", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsEnabled = false;

                var result = await _apiService.ReconcileCashAsync(_shift.Id, new ReconcileCashRequest
                {
                    ActualCash = actual,
                    Comment = CommentTextBox.Text.Trim(),
                    CountedById = App.CurrentUser?.Id,
                    CountedBy = App.CurrentUser?.FullName ?? "неизвестно"
                });

                var msg = result.Difference == 0
                    ? "Пересчёт зафиксирован: касса сходится."
                    : $"Пересчёт зафиксирован: {(result.Difference < 0 ? "недостача" : "излишек")} {Math.Abs(result.Difference):N0} ₽.";

                MessageBox.Show(msg, "X-Отчет", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось сохранить пересчёт: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}