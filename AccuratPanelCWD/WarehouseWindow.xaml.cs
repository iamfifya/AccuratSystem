using AccuratPanelCWD.Services;
using AccuratSystem.Contracts.Enums;
using AccuratSystem.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;

namespace AccuratPanelCWD
{
    public partial class WarehouseWindow : Window
    {
        private readonly ApiService _apiService = new ApiService();
        private List<StockItem> _items = new List<StockItem>();
        private List<StockCategory> _categories = new List<StockCategory>();
        private StockItem _selected;
        private bool _isNew;

        private class UnitOption
        {
            public StockUnit Value { get; set; }
            public string Name { get; set; }
        }

        private static readonly List<UnitOption> Units = new List<UnitOption>
        {
            new UnitOption { Value = StockUnit.Piece,    Name = "шт" },
            new UnitOption { Value = StockUnit.Liter,    Name = "л"  },
            new UnitOption { Value = StockUnit.Kilogram, Name = "кг" },
            new UnitOption { Value = StockUnit.Meter,    Name = "м"  },
        };

        public WarehouseWindow()
        {
            InitializeComponent();
            UnitCombo.ItemsSource = Units;
            PurchaseUnitCombo.ItemsSource = Units;
            _ = LoadAllAsync();
        }

        private async Task LoadAllAsync()
        {
            try
            {
                IsEnabled = false;
                _categories = await _apiService.GetStockCategoriesAsync();

                // Фильтр: добавляем служебный пункт «все категории» (Id = 0)
                var filterOptions = new List<StockCategory> { new StockCategory { Id = 0, Name = "🌐 Все категории" } };
                filterOptions.AddRange(_categories);
                CategoryFilterCombo.ItemsSource = filterOptions;

                // В форму редактирования — только реальные категории
                CategoryCombo.ItemsSource = _categories;

                await LoadItemsAsync();
                CategoryCombo.ItemsSource = _categories;
                CategoryFilterCombo.ItemsSource = _categories;
                await LoadItemsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки склада: {ex.Message}", "Ошибка");
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private async Task LoadItemsAsync()
        {
            var raw = CategoryFilterCombo.SelectedValue as int?;
            var categoryId = (raw.HasValue && raw.Value > 0) ? raw.Value : (int?)null;  // 0 = «все»
            _items = await _apiService.GetStockItemsAsync(categoryId, SearchTextBox.Text);
            ItemsGrid.ItemsSource = null;
            ItemsGrid.ItemsSource = _items;
        }

        private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => await LoadItemsAsync();

        private async void CategoryFilter_Changed(object sender, RoutedEventArgs e) => await LoadItemsAsync();

        private void ItemsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ItemsGrid.SelectedItem is StockItem item)
            {
                _selected = item;
                _isNew = false;
                FillForm(item);
            }
        }

        private void FillForm(StockItem i)
        {
            EditTitleText.Text = $"✏ {i.Name}";
            NameTextBox.Text = i.Name;
            ArticleTextBox.Text = i.Article;
            CategoryCombo.SelectedValue = i.CategoryId;
            UnitCombo.SelectedValue = i.Unit;
            PurchaseUnitCombo.SelectedValue = i.PurchaseUnit;
            RatioTextBox.Text = i.PurchaseRatio.ToString(System.Globalization.CultureInfo.InvariantCulture);
            MinStockTextBox.Text = i.MinStock.ToString(System.Globalization.CultureInfo.InvariantCulture);
            CostTextBox.Text = i.LastPurchaseCost.ToString(System.Globalization.CultureInfo.InvariantCulture);
            NotesTextBox.Text = i.Notes;
            IsActiveCheckBox.IsChecked = i.IsActive;
            EditPanel.IsEnabled = true;
        }

        private void AddItem_Click(object sender, RoutedEventArgs e)
        {
            _selected = new StockItem { PurchaseRatio = 1m, IsActive = true };
            _isNew = true;
            EditTitleText.Text = "➕ Новая позиция";
            NameTextBox.Text = "";
            ArticleTextBox.Text = "";
            CategoryCombo.SelectedValue = null;
            UnitCombo.SelectedValue = StockUnit.Piece;
            PurchaseUnitCombo.SelectedValue = StockUnit.Piece;
            RatioTextBox.Text = "1";
            MinStockTextBox.Text = "0";
            CostTextBox.Text = "0";
            NotesTextBox.Text = "";
            IsActiveCheckBox.IsChecked = true;
            EditPanel.IsEnabled = true;
            NameTextBox.Focus();
        }

        private async void SaveItem_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("Укажите название позиции!", "Внимание");
                return;
            }

            decimal ratio = ParseDecimal(RatioTextBox.Text, 1m);
            if (ratio <= 0)
            {
                MessageBox.Show("Коэффициент приёмки должен быть больше нуля!", "Внимание");
                return;
            }

            try
            {
                IsEnabled = false;
                _selected.Name = NameTextBox.Text.Trim();
                _selected.Article = ArticleTextBox.Text?.Trim() ?? "";
                _selected.CategoryId = CategoryCombo.SelectedValue as int?;
                _selected.Unit = UnitCombo.SelectedValue is StockUnit u ? u : StockUnit.Piece;
                _selected.PurchaseUnit = PurchaseUnitCombo.SelectedValue is StockUnit pu ? pu : StockUnit.Piece;
                _selected.PurchaseRatio = ratio;
                _selected.MinStock = ParseDecimal(MinStockTextBox.Text, 0m);
                _selected.LastPurchaseCost = ParseDecimal(CostTextBox.Text, 0m);
                _selected.Notes = NotesTextBox.Text ?? "";
                _selected.IsActive = IsActiveCheckBox.IsChecked ?? true;

                if (_isNew) await _apiService.CreateStockItemAsync(_selected);
                else await _apiService.UpdateStockItemAsync(_selected);

                EditPanel.IsEnabled = false;
                await LoadItemsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка");
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private async void ArchiveItem_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null || _isNew) return;
            var r = MessageBox.Show($"Перенести «{_selected.Name}» в архив?\nПозиция скроется из списков, но история движений сохранится.",
                                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
            try
            {
                IsEnabled = false;
                await _apiService.ArchiveStockItemAsync(_selected.Id);
                EditPanel.IsEnabled = false;
                await LoadItemsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка архивации: {ex.Message}", "Ошибка");
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private void Categories_Click(object sender, RoutedEventArgs e)
        {
            var win = new StockCategoriesWindow();
            win.Closed += async (s, args) => await LoadAllAsync();
            win.ShowDialog();
        }

        private static decimal ParseDecimal(string s, decimal fallback) =>
            decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                             System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;

        private void DecimalOnly_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
            e.Handled = !Regex.IsMatch(e.Text, @"^[0-9.,]+$");

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Balances_Click(object sender, RoutedEventArgs e) => new StockBalancesWindow().ShowDialog();
        private void Documents_Click(object sender, RoutedEventArgs e) => new StockDocumentsWindow().ShowDialog();
    }
}