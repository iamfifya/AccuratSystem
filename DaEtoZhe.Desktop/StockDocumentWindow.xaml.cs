using DaEtoZhe.Desktop.Services;
using DaEtoZhe.Contracts.Enums;
using DaEtoZhe.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DaEtoZhe.Desktop
{
    public partial class StockDocumentWindow : Window
    {
        private readonly ApiService _apiService = new ApiService();
        private readonly int _branchId;
        private readonly List<StockItem> _items = new List<StockItem>();
        private readonly Dictionary<int, StockBalance> _balances = new Dictionary<int, StockBalance>();
        private readonly ObservableCollection<DocLineVm> _lines = new ObservableCollection<DocLineVm>();

        private class TypeOption
        {
            public StockDocumentType Value { get; set; }
            public string Name { get; set; }
        }

        private class DocLineVm
        {
            public StockItem Item { get; set; }
            public decimal Qty { get; set; }
            public decimal Cost { get; set; }
            public decimal Sum => Qty * Cost;
        }

        public StockDocumentWindow(int branchId)
        {
            InitializeComponent();
            _branchId = branchId;
            LinesGrid.ItemsSource = _lines;
            TypeCombo.ItemsSource = new List<TypeOption>
            {
                new TypeOption { Value = StockDocumentType.Receipt, Name = "📥 Приход" },
                new TypeOption { Value = StockDocumentType.WriteOff, Name = "📤 Списание" },
            };
            TypeCombo.SelectedValue = StockDocumentType.Receipt;
            _ = LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            _items.Clear();
            _items.AddRange(await _apiService.GetStockItemsAsync());
            ItemCombo.ItemsSource = _items;

            _balances.Clear();
            foreach (var b in await _apiService.GetStockBalancesAsync(_branchId))
                _balances[b.ItemId] = b;
        }

        private void TypeCombo_Changed(object sender, RoutedEventArgs e)
        {
            var isReceipt = TypeCombo.SelectedValue is StockDocumentType t && t == StockDocumentType.Receipt;
            TitleText.Text = isReceipt ? "📥 Приход" : "📤 Списание";
            SupplierTextBox.IsEnabled = isReceipt;
            CostTextBox.IsReadOnly = !isReceipt;   // при списании цена = скользящая средняя
            CostLabelText.Text = isReceipt ? "Цена закупки" : "Ср. цена (авто)";
            ItemCombo_Changed(null, null);
        }

        private void ItemCombo_Changed(object sender, RoutedEventArgs e)
        {
            if (ItemCombo.SelectedValue is not int itemId)
            {
                QtyUnitText.Text = "";
                return;
            }
            var item = _items.FirstOrDefault(i => i.Id == itemId);
            if (item == null)
            {
                QtyUnitText.Text = "";
                return;
            }

            QtyUnitText.Text = $"{StockUnitNames.Get(item.Unit)}  (1 {StockUnitNames.Get(item.PurchaseUnit)} = {item.PurchaseRatio:0.###} {StockUnitNames.Get(item.Unit)})";

            var isReceipt = TypeCombo.SelectedValue is StockDocumentType t && t == StockDocumentType.Receipt;
            var cost = isReceipt
                ? item.LastPurchaseCost
                : (_balances.TryGetValue(itemId, out var b) ? b.AvgCost : item.LastPurchaseCost);
            CostTextBox.Text = cost.ToString(CultureInfo.InvariantCulture);
        }

        private void AddLine_Click(object sender, RoutedEventArgs e)
        {
            if (ItemCombo.SelectedValue is not int itemId)
            {
                MessageBox.Show("Выберите позицию!", "Внимание");
                return;
            }
            var item = _items.FirstOrDefault(i => i.Id == itemId);
            var qty = ParseDecimal(QtyTextBox.Text, 0m);
            if (qty <= 0)
            {
                MessageBox.Show("Количество должно быть больше нуля!", "Внимание");
                return;
            }
            if (_lines.Any(l => l.Item.Id == itemId))
            {
                MessageBox.Show("Эта позиция уже добавлена в документ!", "Внимание");
                return;
            }

            _lines.Add(new DocLineVm
            {
                Item = item,
                Qty = qty,
                Cost = ParseDecimal(CostTextBox.Text, 0m)
            });
            QtyTextBox.Text = "";
            ItemCombo.ClearSelection();
            QtyTextBox.Text = "";
            QtyUnitText.Text = "";
        }

        private void RemoveLine_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is DocLineVm line) _lines.Remove(line);
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!_lines.Any())
            {
                MessageBox.Show("Добавьте хотя бы одну позицию!", "Внимание");
                return;
            }

            var isReceipt = TypeCombo.SelectedValue is StockDocumentType t && t == StockDocumentType.Receipt;

            var doc = new StockDocument
            {
                BranchId = _branchId,
                Type = isReceipt ? StockDocumentType.Receipt : StockDocumentType.WriteOff,
                SupplierName = SupplierTextBox.Text?.Trim() ?? "",
                Comment = CommentTextBox.Text?.Trim() ?? "",
                CreatedBy = App.CurrentUser?.DisplayString ?? "Неизвестно",
                Movements = _lines.Select(l => new StockMovement
                {
                    ItemId = l.Item.Id,
                    Quantity = l.Qty,
                    CostPrice = l.Cost
                }).ToList()
            };

            try
            {
                IsEnabled = false;
                var saved = await _apiService.CreateStockDocumentAsync(doc);
                MessageBox.Show($"Документ {saved.Number} проведён!", "Успех");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка проведения: {ex.Message}", "Ошибка");
            }
            finally
            {
                IsEnabled = true;
            }
        }


        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

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