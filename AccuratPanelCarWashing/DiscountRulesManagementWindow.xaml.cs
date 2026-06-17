using AccuratSystem.Contracts.Models;
using AccuratPanelCarWashing.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace AccuratPanelCarWashing
{
    public partial class DiscountRulesManagementWindow : Window
    {
        private readonly ApiService _apiService = new ApiService();

        public DiscountRulesManagementWindow()
        {
            InitializeComponent();
            // По умолчанию выбираем "Процент"if (RuleTypeCombo.Items.Count > 0)
            if (RuleTypeCombo.Items.Count > 0)
            {
                RuleTypeCombo.SelectedItem = RuleTypeCombo.Items[0];
            }
            _ = LoadRulesAsync();
        }

        private async Task LoadRulesAsync()
        {
            try
            {
                var rules = await _apiService.GetDiscountRulesAsync();
                RulesListBox.ItemsSource = rules;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке правил: {ex.Message}");
            }
        }

        private async void SaveRule_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(RuleNameTextBox.Text))
            {
                MessageBox.Show("Введите название правила!", "Внимание");
                return;
            }

            if (!decimal.TryParse(RuleValueTextBox.Text, out decimal val))
            {
                MessageBox.Show("Введите корректное числовое значение!", "Ошибка");
                return;
            }

            // Определяем тип скидки из комбобокса
            bool isPercentage = Convert.ToBoolean(RuleTypeCombo.SelectedValue ?? true);

            var newRule = new DiscountRule
            {
                Name = RuleNameTextBox.Text.Trim(),
                Value = val,
                IsPercentage = isPercentage
            };

            try
            {
                this.IsEnabled = false;
                await _apiService.CreateDiscountRuleAsync(newRule);

                // Очищаем поля
                RuleNameTextBox.Clear();
                RuleValueTextBox.Clear();

                await LoadRulesAsync();
                MessageBox.Show("Правило успешно создано!", "Успех");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}");
            }
            finally
            {
                this.IsEnabled = true;
            }
        }

        private async void DeleteRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DiscountRule rule)
            {
                var result = MessageBox.Show($"Удалить правило '{rule.Name}'?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                try
                {
                    this.IsEnabled = false;
                    await _apiService.DeleteDiscountRuleAsync(rule.Id);
                    await LoadRulesAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении: {ex.Message}");
                }
                finally
                {
                    this.IsEnabled = true;
                }
            }
        }
    }
}
