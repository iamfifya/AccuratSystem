using AccuratPanelCWD.Models;
using AccuratPanelCWD.Services;
using AccuratSystem.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AccuratPanelCWD
{
    public partial class StockBalancesWindow : Window
    {
        private readonly ApiService _apiService = new ApiService();

        public StockBalancesWindow()
        {
            InitializeComponent();
            _ = LoadBranchesAsync();
        }

        private async System.Threading.Tasks.Task LoadBranchesAsync()
        {
            var branches = await _apiService.GetBranchesAsync();
            BranchCombo.ItemsSource = branches;
            BranchCombo.SelectedValue = branches.Any(b => b.Id == AppSettings.CurrentBranchId)
                ? AppSettings.CurrentBranchId
                : branches.FirstOrDefault()?.Id;
            await LoadBalancesAsync();
        }

        private async System.Threading.Tasks.Task LoadBalancesAsync()
        {
            if (BranchCombo.SelectedValue is not int branchId) return;
            try
            {
                var balances = await _apiService.GetStockBalancesAsync(branchId);
                BalancesGrid.ItemsSource = null;
                BalancesGrid.ItemsSource = balances;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки остатков: {ex.Message}", "Ошибка");
            }
        }

        private async void BranchCombo_Changed(object sender, RoutedEventArgs e) => await LoadBalancesAsync();
        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadBalancesAsync();

        private void BalancesGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.DataContext is StockBalance b && b.Item != null)
            {
                if (b.Quantity < 0)
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)) { Opacity = 0.18 };
                else if (b.Quantity <= b.Item.MinStock)
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(230, 126, 34)) { Opacity = 0.18 };
                else
                    e.Row.Background = Brushes.Transparent;
            }
        }
    }
}