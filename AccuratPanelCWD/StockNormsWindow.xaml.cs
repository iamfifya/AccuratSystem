using AccuratPanelCWD.Services;
using AccuratSystem.Contracts.Enums;
using AccuratSystem.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AccuratPanelCWD
{
    public partial class StockNormsWindow : Window
    {
        private readonly ApiService _apiService = new ApiService();
        private readonly int _serviceId;
        private List<ServiceStockNorm> _norms = new List<ServiceStockNorm>();
        private List<StockItem> _items = new List<StockItem>();

        public StockNormsWindow(int serviceId, string serviceName)
        {
            InitializeComponent();
            _serviceId = serviceId;
            TitleText.Text = $"📦 Нормы списания: {serviceName}";
            _ = LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            try
            {
                _norms = await _apiService.GetStockNormsAsync(_serviceId);
                _items = await _apiService.GetStockItemsAsync();

                // В комбобокс — только позиции, для которых нормы ещё не заданы
                var usedIds = new HashSet<int>(_norms.Select(n => n.ItemId));
                ItemCombo.ItemsSource = _items.Where(i => !usedIds.Contains(i.Id)).ToList();

                NormsGrid.ItemsSource = null;
                NormsGrid.ItemsSource = _norms;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки норм: {ex.Message}", "Ошибка");
            }
        }

        private void ItemCombo_Changed(object sender, RoutedEventArgs e)
        {
            QtyUnitText.Text = ItemCombo.SelectedValue is int id
                ? StockUnitNames.Get(_items.FirstOrDefault(i => i.Id == id)?.Unit ?? StockUnit.Piece)
                : "";
        }

        private void NormsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NormsGrid.SelectedItem is ServiceStockNorm norm)
            {
                QtyTextBox.Text = norm.Quantity.ToString(CultureInfo.InvariantCulture);
                QtyUnitText.Text = norm.Item != null ? StockUnitNames.Get(norm.Item.Unit) : "";
            }
        }

        private async void AddNorm_Click(object sender, RoutedEventArgs e)
        {
            if (ItemCombo.SelectedValue is not int itemId)
            {
                MessageBox.Show("Выберите позицию!", "Внимание");
                return;
            }
            var qty = ParseDecimal(QtyTextBox.Text, 0m);
            if (qty <= 0)
            {
                MessageBox.Show("Расход должен быть больше нуля!", "Внимание");
                return;
            }
            try
            {
                await _apiService.CreateStockNormAsync(new ServiceStockNorm
                {
                    ServiceId = _serviceId,
                    ItemId = itemId,
                    Quantity = qty
                });
                QtyTextBox.Text = "";
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления: {ex.Message}", "Ошибка");
            }
        }

        private async void UpdateNorm_Click(object sender, RoutedEventArgs e)
        {
            if (NormsGrid.SelectedItem is not ServiceStockNorm norm)
            {
                MessageBox.Show("Выберите норму в списке!", "Внимание");
                return;
            }
            var qty = ParseDecimal(QtyTextBox.Text, 0m);
            if (qty <= 0)
            {
                MessageBox.Show("Расход должен быть больше нуля!", "Внимание");
                return;
            }
            try
            {
                norm.Quantity = qty;
                await _apiService.UpdateStockNormAsync(norm);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления: {ex.Message}", "Ошибка");
            }
        }

        private async void DeleteNorm_Click(object sender, RoutedEventArgs e)
        {
            if (NormsGrid.SelectedItem is not ServiceStockNorm norm) return;
            var r = MessageBox.Show($"Удалить норму «{norm.Item?.Name}»?", "Подтверждение",
                                    MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
            try
            {
                await _apiService.DeleteStockNormAsync(norm.Id);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void DecimalOnly_PreviewTextInput(object sender, TextCompositionEventArgs ev) =>
            ev.Handled = !System.Text.RegularExpressions.Regex.IsMatch(ev.Text, @"^[0-9.,]+$");

        private static decimal ParseDecimal(string s, decimal fallback)
        {
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            var norm = s.Trim().Replace(',', '.');   // русская запятая = точка
            return decimal.TryParse(norm, System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }
    }
}