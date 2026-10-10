using DaEtoZhe.Contracts.DTOs;
using DaEtoZhe.Contracts.Enums;
using DaEtoZhe.Contracts.Models;
using DaEtoZhe.Desktop.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static DaEtoZhe.Contracts.DTOs.UpdateRepairWorkDto;

namespace DaEtoZhe.Desktop
{
    public partial class VehicleRepairWindow : Window
    {
        private readonly ApiService _apiService = new ApiService();
        private readonly Vehicle _vehicle;

        private List<StockItem> _items = new List<StockItem>();
        private readonly Dictionary<int, StockBalance> _balances = new Dictionary<int, StockBalance>();
        private List<User> _mechanics = new List<User>();
        private List<VehicleDefect> _defects = new List<VehicleDefect>();
        private VehicleRepairWork _editingWork;

        private List<VehicleMechanicShare> _shares = new List<VehicleMechanicShare>();
        private VehicleMechanicShare _editingShare;
        private decimal _laborCost;   // пулл ЗП для превью

        private class StatusOption
        {
            public VehicleWorkStatus Value { get; set; }
            public string Name { get; set; }
        }

        public VehicleRepairWindow(Vehicle vehicle)
        {
            InitializeComponent();
            _vehicle = vehicle;

            // Подписка на событие CustomComboBox
            ItemCombo.SelectionChanged += ItemCombo_Changed;

            TitleText.Text = $"🔧 Ремонт: {vehicle.Make} {vehicle.Model} ({vehicle.LicensePlate})";
            VehicleInfoText.Text = $"VIN {vehicle.Vin} | статус: {vehicle.Status} | филиал: {(vehicle.BranchId ?? 0)}";

            WorkStatusCombo.ItemsSource = new List<StatusOption>
            {
                new StatusOption { Value = VehicleWorkStatus.Planned, Name = "План" },
                new StatusOption { Value = VehicleWorkStatus.InProgress, Name = "В работе" },
                new StatusOption { Value = VehicleWorkStatus.Done, Name = "Готово" },
            };
            WorkStatusCombo.SelectedValue = VehicleWorkStatus.Planned;

            _ = LoadDictionariesAsync();
        }

        private async System.Threading.Tasks.Task LoadDictionariesAsync()
        {
            try
            {
                _items = await _apiService.GetStockItemsAsync();
                ItemCombo.ItemsSource = _items;

                if (_vehicle.BranchId.HasValue)
                {
                    _balances.Clear();
                    foreach (var b in await _apiService.GetStockBalancesAsync(_vehicle.BranchId.Value))
                        _balances[b.ItemId] = b;
                }

                var users = await _apiService.GetUsersAsync();
                _mechanics = users.Where(u => u.IsActive).ToList();
                MechanicCombo.ItemsSource = _mechanics;
                ShareMechanicCombo.ItemsSource = _mechanics;

                _defects = await _apiService.GetVehicleDefectsAsync(_vehicle.Id);
                DefectCombo.ItemsSource = _defects.Where(d => !d.IsFixed).ToList();

                await LoadSummaryAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки справочников: {ex.Message}", "Ошибка");
            }
        }

        private void UpdateShareSum()
        {
            var sum = _shares.Sum(s => s.SharePercent);
            ShareSumText.Text = $"Сумма долей: {sum:0.##}% (пулл ЗП: {_laborCost:N0} ₽)";
            ShareSumText.Foreground = sum == 100m
                ? (TryFindResource("AccentGreen") as Brush ?? Brushes.Green)
                : (TryFindResource("AccentRed") as Brush ?? Brushes.Red);
        }

        private void SharesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _editingShare = SharesGrid.SelectedItem as VehicleMechanicShare;
            SaveShareButton.IsEnabled = _editingShare != null;
            AddShareButton.Content = _editingShare != null ? "➕ Новый" : "➕ Добавить";
            if (_editingShare == null) return;
            ShareMechanicCombo.SelectedValue = _editingShare.MechanicId;
            SharePercentTextBox.Text = _editingShare.SharePercent.ToString(CultureInfo.InvariantCulture);
        }

        private async void AddShare_Click(object sender, RoutedEventArgs e)
        {
            if (ShareMechanicCombo.SelectedValue is not int mechanicId)
            {
                MessageBox.Show("Выберите механика!", "Внимание");
                return;
            }
            var percent = ParseDecimal(SharePercentTextBox.Text, 0m);
            if (percent <= 0)
            {
                MessageBox.Show("Процент должен быть больше нуля!", "Внимание");
                return;
            }
            try
            {
                IsEnabled = false;
                await _apiService.AddVehicleShareAsync(_vehicle.Id, new AddVehicleShareDto
                {
                    MechanicId = mechanicId,
                    SharePercent = percent
                });
                ClearShareForm();
                await LoadSummaryAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления доли: {ex.Message}", "Ошибка");
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private async void SaveShare_Click(object sender, RoutedEventArgs e)
        {
            if (_editingShare == null) return;
            if (ShareMechanicCombo.SelectedValue is not int mechanicId) return;
            var percent = ParseDecimal(SharePercentTextBox.Text, 0m);
            if (percent <= 0)
            {
                MessageBox.Show("Процент должен быть больше нуля!", "Внимание");
                return;
            }
            try
            {
                IsEnabled = false;
                await _apiService.UpdateVehicleShareAsync(new UpdateVehicleShareDto
                {
                    Id = _editingShare.Id,
                    MechanicId = mechanicId,
                    SharePercent = percent
                });
                ClearShareForm();
                await LoadSummaryAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения доли: {ex.Message}", "Ошибка");
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private async void DeleteShare_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is VehicleMechanicShare share)
            {
                var r = MessageBox.Show($"Убрать {share.Mechanic?.FullName} из бригады?", "Подтверждение",
                                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
                try
                {
                    IsEnabled = false;
                    await _apiService.DeleteVehicleShareAsync(share.Id);
                    ClearShareForm();
                    await LoadSummaryAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления доли: {ex.Message}", "Ошибка");
                }
                finally
                {
                    IsEnabled = true;
                }
            }
        }

        private void ClearShareForm()
        {
            _editingShare = null;
            SharesGrid.SelectedItem = null;
            SharePercentTextBox.Text = "";
            SaveShareButton.IsEnabled = false;
            AddShareButton.Content = "➕ Добавить";
        }

        private async System.Threading.Tasks.Task LoadSummaryAsync()
        {
            var summary = await _apiService.GetVehicleRepairAsync(_vehicle.Id);

            PartsGrid.ItemsSource = null;
            PartsGrid.ItemsSource = summary.Parts;
            WorksGrid.ItemsSource = null;
            WorksGrid.ItemsSource = summary.Works;

            PartsCostText.Text = $"{summary.PartsCost:N0} ₽";
            LaborCostText.Text = $"{summary.LaborCost:N0} ₽";
            TotalCostText.Text = $"{summary.TotalCost:N0} ₽";
            EstimateText.Text = $"{summary.EstimateCost:N0} ₽";
            OverrunText.Text = $"{summary.Overrun:+0;-0;0} ₽";
            OverrunText.Foreground = summary.Overrun > 0
                ? (TryFindResource("AccentRed") as Brush ?? Brushes.Red)
                : (TryFindResource("AccentGreen") as Brush ?? Brushes.Green);

            _shares = summary.Shares;
            _laborCost = summary.LaborCost;
            SharesGrid.ItemsSource = null;
            SharesGrid.ItemsSource = _shares;
            UpdateShareSum();
        }

        private void ItemCombo_Changed(object sender, RoutedEventArgs e)
        {
            if (ItemCombo.SelectedValue is int itemId && _balances.TryGetValue(itemId, out var b))
            {
                StockHintText.Text = $"Остаток на складе филиала: {b.Quantity:0.###} {StockUnitNames.Get(b.Item?.Unit ?? StockUnit.Piece)}";
            }
            else
            {
                StockHintText.Text = _vehicle.BranchId.HasValue ? "Остаток: 0 (будет минус)" : "Филиал не указан";
            }
        }

        private async void AddPart_Click(object sender, RoutedEventArgs e)
        {
            if (ItemCombo.SelectedValue is not int itemId)
            {
                MessageBox.Show("Выберите позицию!", "Внимание");
                return;
            }
            var qty = ParseDecimal(PartQtyTextBox.Text, 0m);
            if (qty <= 0)
            {
                MessageBox.Show("Количество должно быть больше нуля!", "Внимание");
                return;
            }
            try
            {
                IsEnabled = false;
                await _apiService.AddVehicleRepairPartAsync(_vehicle.Id, new AddRepairPartDto
                {
                    StockItemId = itemId,
                    Quantity = qty,
                    AddedBy = App.CurrentUser?.DisplayString ?? "Ремонт"
                });
                PartQtyTextBox.Text = "";
                await LoadDictionariesAsync();   // остатки изменились — обновляем подсказки
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка списания: {ex.Message}", "Ошибка");
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private async void ReturnPart_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is VehicleRepairPart part)
            {
                var r = MessageBox.Show($"Вернуть «{part.StockItem?.Name}» на склад?\nБудет создано сторно-движение.",
                                        "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
                try
                {
                    IsEnabled = false;
                    await _apiService.DeleteVehicleRepairPartAsync(part.Id);
                    await LoadDictionariesAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка возврата: {ex.Message}", "Ошибка");
                }
                finally
                {
                    IsEnabled = true;
                }
            }
        }

        private void WorksGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _editingWork = WorksGrid.SelectedItem as VehicleRepairWork;
            SaveWorkButton.Content = _editingWork != null ? "💾 Сохранить" : "➕ Добавить";
            if (_editingWork == null) return;

            MechanicCombo.SelectedValue = _editingWork.MechanicId;
            DefectCombo.SelectedValue = _editingWork.VehicleDefectId;
            WorkDescTextBox.Text = _editingWork.Description;
            PlannedHoursTextBox.Text = _editingWork.PlannedHours.ToString(CultureInfo.InvariantCulture);
            ActualHoursTextBox.Text = _editingWork.ActualHours.ToString(CultureInfo.InvariantCulture);
            RateTextBox.Text = _editingWork.HourlyRate.ToString(CultureInfo.InvariantCulture);
            WorkStatusCombo.SelectedValue = _editingWork.Status;
        }

        private async void SaveWork_Click(object sender, RoutedEventArgs e)
        {
            if (MechanicCombo.SelectedValue is not int mechanicId)
            {
                MessageBox.Show("Выберите механика!", "Внимание");
                return;
            }
            if (string.IsNullOrWhiteSpace(WorkDescTextBox.Text))
            {
                MessageBox.Show("Опишите работу!", "Внимание");
                return;
            }

            var defectId = DefectCombo.SelectedValue as int?;
            var status = WorkStatusCombo.SelectedValue is VehicleWorkStatus s ? s : VehicleWorkStatus.Planned;

            try
            {
                IsEnabled = false;
                if (_editingWork == null)
                {
                    await _apiService.AddVehicleRepairWorkAsync(_vehicle.Id, new AddRepairWorkDto
                    {
                        MechanicId = mechanicId,
                        VehicleDefectId = defectId,
                        Description = WorkDescTextBox.Text.Trim(),
                        PlannedHours = ParseDecimal(PlannedHoursTextBox.Text, 0m),
                        ActualHours = ParseDecimal(ActualHoursTextBox.Text, 0m),
                        HourlyRate = ParseDecimal(RateTextBox.Text, 0m),
                        Status = status
                    });
                }
                else
                {
                    await _apiService.UpdateVehicleRepairWorkAsync(new UpdateRepairWorkDto
                    {
                        Id = _editingWork.Id,
                        MechanicId = mechanicId,
                        VehicleDefectId = defectId,
                        Description = WorkDescTextBox.Text.Trim(),
                        PlannedHours = ParseDecimal(PlannedHoursTextBox.Text, 0m),
                        ActualHours = ParseDecimal(ActualHoursTextBox.Text, 0m),
                        HourlyRate = ParseDecimal(RateTextBox.Text, 0m),
                        Status = status
                    });
                }
                ClearWorkForm();
                await LoadDictionariesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения работы: {ex.Message}", "Ошибка");
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private async void DeleteWork_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is VehicleRepairWork work)
            {
                var r = MessageBox.Show($"Удалить работу «{work.Description}»?", "Подтверждение",
                                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
                try
                {
                    IsEnabled = false;
                    await _apiService.DeleteVehicleRepairWorkAsync(work.Id);
                    ClearWorkForm();
                    await LoadDictionariesAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка");
                }
                finally
                {
                    IsEnabled = true;
                }
            }
        }

        private void ClearWorkForm()
        {
            _editingWork = null;
            WorksGrid.SelectedItem = null;
            SaveWorkButton.Content = "➕ Добавить";
            WorkDescTextBox.Text = "";
            PlannedHoursTextBox.Text = "";
            ActualHoursTextBox.Text = "";
            WorkStatusCombo.SelectedValue = VehicleWorkStatus.Planned;
        }

        private async void Finish_Click(object sender, RoutedEventArgs e)
        {
            var r = MessageBox.Show("Завершить ремонт?\nСтатус станет «Предпродажная подготовка».",
                                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
            try
            {
                IsEnabled = false;
                await _apiService.FinishVehicleRepairAsync(_vehicle.Id);
                MessageBox.Show("Ремонт завершён!", "Успех");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка завершения: {ex.Message}", "Ошибка");
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadDictionariesAsync();
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void DecimalOnly_PreviewTextInput(object sender, TextCompositionEventArgs ev) =>
            ev.Handled = !Regex.IsMatch(ev.Text, @"^[0-9.,]+$");

        private static decimal ParseDecimal(string s, decimal fallback)
        {
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            var norm = s.Trim().Replace(',', '.');
            return decimal.TryParse(norm, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }
    }
}