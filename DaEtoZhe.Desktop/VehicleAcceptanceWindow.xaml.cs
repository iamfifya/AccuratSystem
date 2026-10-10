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

namespace DaEtoZhe.Desktop
{
    public partial class VehicleAcceptanceWindow : Window
    {
        private readonly ApiService _apiService = new ApiService();
        private readonly Vehicle _vehicle;
        private List<VehicleDefect> _defects = new List<VehicleDefect>();
        private VehicleDefect _editing;   // null = режим добавления

        private class Option<T>
        {
            public T Value { get; set; }
            public string Name { get; set; }
        }

        public VehicleAcceptanceWindow(Vehicle vehicle)
        {
            InitializeComponent();
            _vehicle = vehicle;

            TitleText.Text = $"📋 Приёмка: {vehicle.Make} {vehicle.Model}";
            VehicleInfoText.Text = $"{vehicle.LicensePlate} | VIN {vehicle.Vin} | {vehicle.Year} г. | " +
                                   $"куплен за {vehicle.PurchasePrice:N0} ₽ | статус: {vehicle.Status}";

            AreaCombo.ItemsSource = new List<Option<DefectArea>>
            {
                new Option<DefectArea> { Value = DefectArea.Body, Name = "Кузов" },
                new Option<DefectArea> { Value = DefectArea.Engine, Name = "Двигатель" },
                new Option<DefectArea> { Value = DefectArea.Transmission, Name = "Трансмиссия" },
                new Option<DefectArea> { Value = DefectArea.Suspension, Name = "Подвеска" },
                new Option<DefectArea> { Value = DefectArea.Electrical, Name = "Электрика" },
                new Option<DefectArea> { Value = DefectArea.Salon, Name = "Салон" },
                new Option<DefectArea> { Value = DefectArea.Glass, Name = "Стёкла" },
                new Option<DefectArea> { Value = DefectArea.Wheels, Name = "Колёса" },
                new Option<DefectArea> { Value = DefectArea.Other, Name = "Прочее" },
            };
            SeverityCombo.ItemsSource = new List<Option<DefectSeverity>>
            {
                new Option<DefectSeverity> { Value = DefectSeverity.Minor, Name = "Мелкий" },
                new Option<DefectSeverity> { Value = DefectSeverity.Major, Name = "Средний" },
                new Option<DefectSeverity> { Value = DefectSeverity.Critical, Name = "Критичный" },
            };
            AreaCombo.SelectedValue = DefectArea.Body;
            SeverityCombo.SelectedValue = DefectSeverity.Major;

            FillAcceptanceForm();
            _ = LoadDefectsAsync();
        }

        private void FillAcceptanceForm()
        {
            KeysTextBox.Text = _vehicle.KeysCount > 0 ? _vehicle.KeysCount.ToString() : "2";
            PtsCheckBox.IsChecked = _vehicle.HasPts;
            StsCheckBox.IsChecked = _vehicle.HasSts;
            DocsNotesTextBox.Text = _vehicle.DocumentsNotes;
            ConditionTextBox.Text = _vehicle.ConditionSummary;
            if (_vehicle.AcceptedAt != null)
            {
                AcceptedInfoText.Text = $"✅ Акт проведён {_vehicle.AcceptedAt:dd.MM.yyyy HH:mm}, {_vehicle.AcceptedBy}. " +
                                        $"Смета: {_vehicle.EstimateCost:N0} ₽";
            }
        }

        // ═════════════ ДЕФЕКТЫ ═════════════

        private async System.Threading.Tasks.Task LoadDefectsAsync()
        {
            try
            {
                _defects = await _apiService.GetVehicleDefectsAsync(_vehicle.Id);
                DefectsGrid.ItemsSource = null;
                DefectsGrid.ItemsSource = _defects;
                UpdateEstimate();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки дефектов: {ex.Message}", "Ошибка");
            }
        }

        private void UpdateEstimate()
        {
            var total = _defects.Sum(d => d.EstimatedCost);
            var hours = _defects.Sum(d => d.EstimatedHours);
            EstimateText.Text = $"{total:N0} ₽";
            DefectsCountText.Text = $"Дефектов: {_defects.Count} (устранено {_defects.Count(d => d.IsFixed)}) | " +
                                    $"Трудоёмкость: {hours:N1} н/ч";
        }

        private async void AddDefect_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(DefectDescTextBox.Text))
            {
                MessageBox.Show("Опишите дефект!", "Внимание");
                DefectDescTextBox.Focus();
                return;
            }
            try
            {
                await _apiService.CreateVehicleDefectAsync(_vehicle.Id, new CreateVehicleDefectDto
                {
                    Area = (DefectArea)AreaCombo.SelectedValue,
                    Severity = (DefectSeverity)SeverityCombo.SelectedValue,
                    Description = DefectDescTextBox.Text.Trim(),
                    EstimatedCost = ParseDecimal(DefectCostTextBox.Text, 0m),
                    EstimatedHours = ParseDecimal(DefectHoursTextBox.Text, 0m)
                });
                ClearDefectForm();
                await LoadDefectsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления дефекта: {ex.Message}", "Ошибка");
            }
        }

        private async void UpdateDefect_Click(object sender, RoutedEventArgs e)
        {
            if (_editing == null) return;
            if (string.IsNullOrWhiteSpace(DefectDescTextBox.Text))
            {
                MessageBox.Show("Опишите дефект!", "Внимание");
                return;
            }
            try
            {
                await _apiService.UpdateVehicleDefectAsync(new UpdateVehicleDefectDto
                {
                    Id = _editing.Id,
                    Area = (DefectArea)AreaCombo.SelectedValue,
                    Severity = (DefectSeverity)SeverityCombo.SelectedValue,
                    Description = DefectDescTextBox.Text.Trim(),
                    EstimatedCost = ParseDecimal(DefectCostTextBox.Text, 0m),
                    EstimatedHours = ParseDecimal(DefectHoursTextBox.Text, 0m),
                    IsFixed = _editing.IsFixed,
                    Notes = _editing.Notes
                });
                ClearDefectForm();
                await LoadDefectsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления дефекта: {ex.Message}", "Ошибка");
            }
        }

        private async void ToggleFixed_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is VehicleDefect defect)
            {
                try
                {
                    await _apiService.UpdateVehicleDefectAsync(new UpdateVehicleDefectDto
                    {
                        Id = defect.Id,
                        Area = defect.Area,
                        Severity = defect.Severity,
                        Description = defect.Description,
                        EstimatedCost = defect.EstimatedCost,
                        EstimatedHours = defect.EstimatedHours,
                        IsFixed = !defect.IsFixed,
                        Notes = defect.Notes
                    });
                    await LoadDefectsAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка");
                }
            }
        }

        private async void DeleteDefect_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is VehicleDefect defect)
            {
                var r = MessageBox.Show($"Удалить дефект «{defect.Description}»?", "Подтверждение",
                                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
                try
                {
                    await _apiService.DeleteVehicleDefectAsync(defect.Id);
                    if (_editing?.Id == defect.Id) ClearDefectForm();
                    await LoadDefectsAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка");
                }
            }
        }

        private void DefectsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _editing = DefectsGrid.SelectedItem as VehicleDefect;
            UpdateDefectButton.IsEnabled = _editing != null;
            if (_editing == null) return;

            DefectDescTextBox.Text = _editing.Description;
            AreaCombo.SelectedValue = _editing.Area;
            SeverityCombo.SelectedValue = _editing.Severity;
            DefectCostTextBox.Text = _editing.EstimatedCost.ToString(CultureInfo.InvariantCulture);
            DefectHoursTextBox.Text = _editing.EstimatedHours.ToString(CultureInfo.InvariantCulture);
        }

        private void ClearDefectForm()
        {
            _editing = null;
            DefectsGrid.SelectedItem = null;
            DefectDescTextBox.Text = "";
            DefectCostTextBox.Text = "";
            DefectHoursTextBox.Text = "";
            AreaCombo.SelectedValue = DefectArea.Body;
            SeverityCombo.SelectedValue = DefectSeverity.Major;
            UpdateDefectButton.IsEnabled = false;
        }

        // ═════════════ АКТ ПРИЁМКИ ═════════════

        private async void Accept_Click(object sender, RoutedEventArgs e)
        {
            var r = MessageBox.Show(
                "Провести акт приёмки?\n\nСтатус станет «Оценен», смета зафиксируется по текущим дефектам.",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            try
            {
                IsEnabled = false;
                var updated = await _apiService.AcceptVehicleAsync(_vehicle.Id, new AcceptVehicleDto
                {
                    KeysCount = ParseInt(KeysTextBox.Text, 0),
                    HasPts = PtsCheckBox.IsChecked == true,
                    HasSts = StsCheckBox.IsChecked == true,
                    DocumentsNotes = DocsNotesTextBox.Text,
                    ConditionSummary = ConditionTextBox.Text,
                    AcceptedBy = App.CurrentUser?.DisplayString ?? "Не указан"
                });
                MessageBox.Show($"Акт проведён. Смета: {updated.EstimateCost:N0} ₽, статус: Оценен.",
                                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка проведения акта: {ex.Message}", "Ошибка");
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        // ═════════════ ХЕЛПЕРЫ ═════════════

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs ev) =>
            ev.Handled = !Regex.IsMatch(ev.Text, @"^[0-9]+$");

        private void DecimalOnly_PreviewTextInput(object sender, TextCompositionEventArgs ev) =>
            ev.Handled = !Regex.IsMatch(ev.Text, @"^[0-9.,]+$");

        private static int ParseInt(string s, int fallback) =>
            int.TryParse(s?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        private static decimal ParseDecimal(string s, decimal fallback)
        {
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            var norm = s.Trim().Replace(',', '.');
            return decimal.TryParse(norm, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }
    }
}