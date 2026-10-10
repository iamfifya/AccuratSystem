using DaEtoZhe.Desktop.Services;
using DaEtoZhe.Contracts.DTOs;
using DaEtoZhe.Contracts.Enums;
using DaEtoZhe.Contracts.Models;
using DaEtoZhe.Desktop.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DaEtoZhe.Desktop
{
    public partial class VehiclesWindow : Window
    {
        private readonly ApiService _apiService;
        private List<Vehicle> _allVehicles;
        private List<Vehicle> _filteredVehicles;

        public VehiclesWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();

            StatusFilter.SelectedIndex = 0; // "Все статусы"

            Loaded += async (s, e) => await LoadVehiclesAsync();
        }

        private async System.Threading.Tasks.Task LoadVehiclesAsync()
        {
            try
            {
                VehicleStatus? statusFilter = null;
                if (StatusFilter.SelectedIndex > 0)
                {
                    var tag = (StatusFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                    if (!string.IsNullOrEmpty(tag) && int.TryParse(tag, out int statusValue))
                    {
                        statusFilter = (VehicleStatus)statusValue;
                    }
                }

                _allVehicles = await _apiService.GetVehiclesAsync(statusFilter);
                ApplyFilters();
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки автомобилей: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilters()
        {
            var searchText = SearchBox.Text?.Trim().ToLower() ?? "";

            _filteredVehicles = string.IsNullOrEmpty(searchText)
                ? _allVehicles
                : _allVehicles.Where(v =>
                    v.Vin.ToLower().Contains(searchText) ||
                    v.LicensePlate.ToLower().Contains(searchText) ||
                    v.Make.ToLower().Contains(searchText) ||
                    v.Model.ToLower().Contains(searchText)).ToList();

            VehiclesGrid.ItemsSource = _filteredVehicles;
        }

        private void UpdateStatistics()
        {
            TotalCountText.Text = _allVehicles.Count.ToString();
            InRepairCountText.Text = _allVehicles.Count(v => v.Status == VehicleStatus.Repair).ToString();
            ForSaleCountText.Text = _allVehicles.Count(v => v.Status == VehicleStatus.Listed).ToString();
            SoldCountText.Text = _allVehicles.Count(v => v.Status == VehicleStatus.Sold).ToString();

            var totalMargin = _allVehicles
                .Where(v => v.Status == VehicleStatus.Sold)
                .Sum(v => v.Margin);

            TotalMarginText.Text = $"{totalMargin:N0} ₽";
        }

        private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                _ = LoadVehiclesAsync();
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allVehicles != null)
            {
                ApplyFilters();
            }
        }

        private async void AddVehicle_Click(object sender, RoutedEventArgs e)
        {
            var editWindow = new VehicleEditWindow(null);
            if (editWindow.ShowDialog() == true)
            {
                try
                {
                    await _apiService.CreateVehicleAsync(editWindow.VehicleDto);
                    await LoadVehiclesAsync();
                    MessageBox.Show("Автомобиль успешно добавлен!", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при добавлении автомобиля: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void EditVehicle_Click(object sender, RoutedEventArgs e)
        {
            if (VehiclesGrid.SelectedItem is Vehicle vehicle)
            {
                var editWindow = new VehicleEditWindow(vehicle);
                if (editWindow.ShowDialog() == true)
                {
                    try
                    {
                        await _apiService.UpdateVehicleAsync(editWindow.UpdateDto);
                        await LoadVehiclesAsync();
                        MessageBox.Show("Автомобиль успешно обновлён!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при обновлении автомобиля: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void ChangeStatus_Click(object sender, RoutedEventArgs e)
        {
            if (VehiclesGrid.SelectedItem is Vehicle vehicle)
            {
                var statusWindow = new ChangeStatusWindow(vehicle.Status);
                if (statusWindow.ShowDialog() == true)
                {
                    try
                    {
                        await _apiService.ChangeVehicleStatusAsync(
                            vehicle.Id,
                            statusWindow.NewStatus,
                            statusWindow.Notes);
                        await LoadVehiclesAsync();
                        MessageBox.Show($"Статус изменён на '{statusWindow.NewStatus}'!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при изменении статуса: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void DeleteVehicle_Click(object sender, RoutedEventArgs e)
        {
            if (VehiclesGrid.SelectedItem is Vehicle vehicle)
            {
                var result = MessageBox.Show(
                    $"Вы уверены, что хотите удалить автомобиль {vehicle.Make} {vehicle.Model} ({vehicle.Vin})?\n\n" +
                    "Это действие нельзя отменить.",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _apiService.DeleteVehicleAsync(vehicle.Id);
                        await LoadVehiclesAsync();
                        MessageBox.Show("Автомобиль успешно удалён!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении автомобиля: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void VehiclesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (VehiclesGrid.SelectedItem is Vehicle vehicle)
            {
                EditVehicle_Click(sender, e);
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadVehiclesAsync();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void Acceptance_Click(object sender, RoutedEventArgs e)
        {
            if (VehiclesGrid.SelectedItem is Vehicle vehicle)
            {
                var win = new VehicleAcceptanceWindow(vehicle);
                win.ShowDialog();
                await LoadVehiclesAsync();   // статус/смета могли измениться
            }
            else
            {
                MessageBox.Show("Выберите автомобиль в списке!", "Внимание");
            }
        }
    }

    #region КОНВЕРТЕРЫ
    // Конвертер статуса в цвет
    public class VehicleStatusToColorConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is VehicleStatus status)
            {
                return status switch
                {
                    VehicleStatus.New => new SolidColorBrush(Color.FromRgb(149, 165, 166)),       // Серый
                    VehicleStatus.Purchase => new SolidColorBrush(Color.FromRgb(52, 152, 219)),   // Синий
                    VehicleStatus.Appraisal => new SolidColorBrush(Color.FromRgb(155, 89, 182)),  // Фиолетовый
                    VehicleStatus.Repair => new SolidColorBrush(Color.FromRgb(230, 126, 34)),     // Оранжевый
                    VehicleStatus.Prep => new SolidColorBrush(Color.FromRgb(241, 196, 15)),       // Жёлтый
                    VehicleStatus.Listed => new SolidColorBrush(Color.FromRgb(52, 152, 219)),     // Синий
                    VehicleStatus.Reserved => new SolidColorBrush(Color.FromRgb(231, 76, 60)),    // Красный
                    VehicleStatus.Sold => new SolidColorBrush(Color.FromRgb(39, 174, 96)),        // Зелёный
                    VehicleStatus.WrittenOff => new SolidColorBrush(Color.FromRgb(44, 62, 80)),   // Тёмно-серый
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Конвертер статуса в текст
    public class VehicleStatusToTextConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is VehicleStatus status)
            {
                return status switch
                {
                    VehicleStatus.New => "Новый",
                    VehicleStatus.Purchase => "Куплен",
                    VehicleStatus.Appraisal => "Оценен",
                    VehicleStatus.Repair => "В ремонте",
                    VehicleStatus.Prep => "Подготовка",
                    VehicleStatus.Listed => "На продаже",
                    VehicleStatus.Reserved => "Зарезервирован",
                    VehicleStatus.Sold => "Продан",
                    VehicleStatus.WrittenOff => "Списан",
                    _ => "Неизвестно"
                };
            }
            return "Неизвестно";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}