using AccuratSystem.Contracts.DTOs;
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
    public partial class UpsellManagementWindow : Window
    {
        private readonly ApiService _apiService;
        private List<Service> _allServices;

        public UpsellManagementWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                // Загружаем услуги для выпадающих списков
                _allServices = await _apiService.GetServicesAsync();
                TriggerServiceCombo.ItemsSource = _allServices;
                SuggestedServiceCombo.ItemsSource = _allServices;

                // Загружаем активные правила
                var rules = await _apiService.GetUpsellRulesAsync();

                // Оборачиваем правила во ViewModel, чтобы показать названия услуг вместо ID
                var displayRules = rules.Select(r => new RuleDisplayModel
                {
                    Rule = r,
                    TriggerServiceName = _allServices.FirstOrDefault(s => s.Id == r.TriggerServiceId)?.Name ?? "Неизвестно",
                    SuggestedServiceName = _allServices.FirstOrDefault(s => s.Id == r.SuggestedServiceId)?.Name ?? "Неизвестно"
                }).ToList();

                RulesListBox.ItemsSource = displayRules;
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
        }

        private async void AddRuleButton_Click(object sender, RoutedEventArgs e)
        {
            if (TriggerServiceCombo.SelectedValue == null || SuggestedServiceCombo.SelectedValue == null)
            {
                MessageBox.Show("Выберите обе услуги!"); return;
            }

            decimal.TryParse(BonusTextBox.Text, out decimal bonus);

            var newRule = new UpsellSuggestion
            {
                TriggerServiceId = (int)TriggerServiceCombo.SelectedValue,
                SuggestedServiceId = (int)SuggestedServiceCombo.SelectedValue,
                Message = MessageTextBox.Text,
                BonusAmount = bonus
            };

            this.IsEnabled = false;
            await _apiService.CreateUpsellRuleAsync(newRule);
            this.IsEnabled = true;

            await LoadDataAsync(); // Перезагружаем список
        }

        private async void DeleteRuleButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is RuleDisplayModel displayRule)
            {
                this.IsEnabled = false;
                await _apiService.DeleteUpsellRuleAsync(displayRule.Rule.Id);
                this.IsEnabled = true;
                await LoadDataAsync();
            }
        }
    }

    public class RuleDisplayModel
    {
        public UpsellSuggestion Rule { get; set; }
        public string TriggerServiceName { get; set; }
        public string SuggestedServiceName { get; set; }
    }
}