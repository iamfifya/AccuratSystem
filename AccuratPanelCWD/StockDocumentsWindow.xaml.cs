using AccuratPanelCWD.Models;
using AccuratPanelCWD.Services;
using AccuratSystem.Contracts.Enums;
using AccuratSystem.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace AccuratPanelCWD
{
    /// <summary>Тип документа по-русски для DataGrid.</summary>
    public class StockDocTypeRuConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is StockDocumentType t)
            {
                switch (t)
                {
                    case StockDocumentType.Receipt: return " Приход";
                    case StockDocumentType.WriteOff: return "📤 Списание";
                    case StockDocumentType.Transfer: return "🔁 Перемещение";
                    case StockDocumentType.Stocktake: return "🧮 Инвентаризация";
                }
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    public partial class StockDocumentsWindow : Window
    {
        private readonly ApiService _apiService = new ApiService();

        private class TypeOption
        {
            public int Value { get; set; }
            public string Name { get; set; }
        }

        public StockDocumentsWindow()
        {
            InitializeComponent();
            TypeFilterCombo.ItemsSource = new List<TypeOption>
            {
                new TypeOption { Value = -1, Name = "Все типы" },
                new TypeOption { Value = 0, Name = "📥 Приход" },
                new TypeOption { Value = 1, Name = "📤 Списание" },
            };
            TypeFilterCombo.SelectedValue = -1;
            _ = LoadBranchesAsync();
        }

        private async System.Threading.Tasks.Task LoadBranchesAsync()
        {
            var branches = await _apiService.GetBranchesAsync();
            BranchCombo.ItemsSource = branches;
            BranchCombo.SelectedValue = branches.Any(b => b.Id == AppSettings.CurrentBranchId)
                ? AppSettings.CurrentBranchId
                : branches.FirstOrDefault()?.Id;
            await LoadDocsAsync();
        }

        private async System.Threading.Tasks.Task LoadDocsAsync()
        {
            if (BranchCombo.SelectedValue is not int branchId) return;
            try
            {
                var filter = TypeFilterCombo.SelectedValue is int v && v >= 0 ? (StockDocumentType?)v : null;
                var docs = await _apiService.GetStockDocumentsAsync(branchId, filter);
                DocsGrid.ItemsSource = null;
                DocsGrid.ItemsSource = docs;
                LinesGrid.ItemsSource = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки документов: {ex.Message}", "Ошибка");
            }
        }

        private async void Filters_Changed(object sender, RoutedEventArgs e) => await LoadDocsAsync();

        private void DocsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LinesGrid.ItemsSource = (DocsGrid.SelectedItem as StockDocument)?.Movements;
        }

        private async void NewDocument_Click(object sender, RoutedEventArgs e)
        {
            if (BranchCombo.SelectedValue is not int branchId) return;
            var win = new StockDocumentWindow(branchId);
            win.Closed += async (s, args) => await LoadDocsAsync();
            win.ShowDialog();
        }
    }
}