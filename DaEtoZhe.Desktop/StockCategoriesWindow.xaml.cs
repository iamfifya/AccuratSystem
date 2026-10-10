using DaEtoZhe.Desktop.Services;
using DaEtoZhe.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace DaEtoZhe.Desktop
{
    public partial class StockCategoriesWindow : Window
    {
        private readonly ApiService _apiService = new ApiService();
        private List<StockCategory> _categories = new List<StockCategory>();
        private StockCategory _selected;

        public StockCategoriesWindow()
        {
            InitializeComponent();
            _ = LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            try
            {
                _categories = await _apiService.GetStockCategoriesAsync();
                CategoriesList.ItemsSource = null;
                CategoriesList.ItemsSource = _categories;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки категорий: {ex.Message}", "Ошибка");
            }
        }

        private void CategoriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selected = CategoriesList.SelectedItem as StockCategory;
            NameTextBox.Text = _selected?.Name ?? "";
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("Укажите название категории!", "Внимание");
                return;
            }
            try
            {
                if (_selected == null)
                {
                    await _apiService.CreateStockCategoryAsync(new StockCategory { Name = NameTextBox.Text.Trim() });
                }
                else
                {
                    _selected.Name = NameTextBox.Text.Trim();
                    await _apiService.UpdateStockCategoryAsync(_selected);
                }
                _selected = null;
                NameTextBox.Text = "";
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка");
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            try
            {
                await _apiService.DeleteStockCategoryAsync(_selected.Id);
                _selected = null;
                NameTextBox.Text = "";
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}