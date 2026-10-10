using DaEtoZhe.Contracts.DTOs;
using DaEtoZhe.Contracts.Enums;
using DaEtoZhe.Contracts.Models;
using DaEtoZhe.Desktop.Models;
using DaEtoZhe.Desktop.Services;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace DaEtoZhe.Desktop
{
    public partial class VehicleEditWindow : Window
    {
        private readonly ApiService _apiService = new ApiService();
        private readonly Vehicle _original;   // null = режим создания
        private readonly bool _isEdit;

        /// <summary>Заполняется в режиме создания.</summary>
        public CreateVehicleDto VehicleDto { get; private set; }

        /// <summary>Заполняется в режиме редактирования.</summary>
        public UpdateVehicleDto UpdateDto { get; private set; }

        public VehicleEditWindow(Vehicle existing)
        {
            InitializeComponent();
            _original = existing;
            _isEdit = existing != null;

            TitleText.Text = _isEdit
                ? $"✏️ {existing.Make} {existing.Model} ({existing.LicensePlate})"
                : "➕ Новый автомобиль";

            if (_isEdit) RepairSection.Visibility = Visibility.Visible;

            _ = LoadBranchesAsync();
        }

        private async System.Threading.Tasks.Task LoadBranchesAsync()
        {
            try
            {
                var branches = await _apiService.GetBranchesAsync();
                BranchCombo.ItemsSource = branches;

                if (_isEdit)
                {
                    FillFromOriginal();
                }
                else
                {
                    // Дефолт — текущий филиал пользователя
                    BranchCombo.SelectedValue = branches.Any(b => b.Id == AppSettings.CurrentBranchId)
                        ? AppSettings.CurrentBranchId
                        : branches.FirstOrDefault()?.Id;
                    YearTextBox.Text = DateTime.Now.Year.ToString();
                    EngineTypeCombo.SelectedIndex = 0;
                    TransmissionCombo.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки филиалов: {ex.Message}", "Ошибка");
            }
        }

        private void FillFromOriginal()
        {
            var v = _original;
            VinTextBox.Text = v.Vin;
            PlateTextBox.Text = v.LicensePlate;
            MakeTextBox.Text = v.Make;
            ModelTextBox.Text = v.Model;
            YearTextBox.Text = v.Year > 0 ? v.Year.ToString() : "";
            MileageTextBox.Text = v.Mileage > 0 ? v.Mileage.ToString() : "";
            ColorTextBox.Text = v.Color;
            EngineVolumeTextBox.Text = v.EngineVolume > 0 ? v.EngineVolume.ToString(CultureInfo.InvariantCulture) : "";
            DefectsTextBox.Text = v.Defects;
            NotesTextBox.Text = v.GeneralNotes;
            PurchasePriceTextBox.Text = v.PurchasePrice.ToString(CultureInfo.InvariantCulture);
            SellerNameTextBox.Text = v.SellerFullName;
            SellerPhoneTextBox.Text = v.SellerPhone;
            SellerPassportTextBox.Text = v.SellerPassport;

            SelectComboByText(EngineTypeCombo, v.EngineType);
            SelectComboByText(TransmissionCombo, v.Transmission);
            BranchCombo.SelectedValue = v.BranchId;

            // Секция ремонта/продажи
            PartsCostTextBox.Text = v.PartsCost.ToString(CultureInfo.InvariantCulture);
            LaborCostTextBox.Text = v.LaborCost.ToString(CultureInfo.InvariantCulture);
            ListingPriceTextBox.Text = v.ListingPrice.ToString(CultureInfo.InvariantCulture);
            SalePriceTextBox.Text = v.SalePrice.ToString(CultureInfo.InvariantCulture);
            RepairNotesTextBox.Text = v.RepairNotes;
            BuyerNameTextBox.Text = v.BuyerFullName;
            BuyerPhoneTextBox.Text = v.BuyerPhone;
            BuyerPassportTextBox.Text = v.BuyerPassport;

            UpdateMarginLabel();
        }

        private static void SelectComboByText(System.Windows.Controls.ComboBox combo, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            foreach (var item in combo.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem ci && (string)ci.Content == value)
                {
                    combo.SelectedItem = ci;
                    return;
                }
            }
        }

        private static string ComboText(System.Windows.Controls.ComboBox combo) =>
            combo.SelectedItem is System.Windows.Controls.ComboBoxItem ci ? (string)ci.Content : string.Empty;

        // ═════════════ СОХРАНЕНИЕ ═════════════

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            int? branchId = BranchCombo.SelectedValue as int?;

            if (!_isEdit)
            {
                VehicleDto = new CreateVehicleDto
                {
                    CompanyId = 0, // сервер подставит CurrentCompanyId
                    BranchId = branchId,
                    Vin = VinTextBox.Text.Trim(),
                    LicensePlate = PlateTextBox.Text.Trim(),
                    Make = MakeTextBox.Text.Trim(),
                    Model = ModelTextBox.Text.Trim(),
                    Year = ParseInt(YearTextBox.Text, DateTime.Now.Year),
                    Mileage = ParseInt(MileageTextBox.Text, 0),
                    Color = ColorTextBox.Text.Trim(),
                    EngineType = ComboText(EngineTypeCombo),
                    EngineVolume = ParseDecimal(EngineVolumeTextBox.Text, 0m),
                    Transmission = ComboText(TransmissionCombo),
                    PurchasePrice = ParseDecimal(PurchasePriceTextBox.Text, 0m),
                    SellerFullName = SellerNameTextBox.Text.Trim(),
                    SellerPhone = SellerPhoneTextBox.Text.Trim(),
                    SellerPassport = SellerPassportTextBox.Text.Trim(),
                    Defects = DefectsTextBox.Text.Trim(),
                    GeneralNotes = NotesTextBox.Text.Trim()
                };
            }
            else
            {
                UpdateDto = new UpdateVehicleDto
                {
                    Id = _original.Id,
                    BranchId = branchId,
                    Status = _original.Status, // статус меняется отдельной кнопкой в списке
                    RepairCost = ParseDecimal(PartsCostTextBox.Text, 0m) + ParseDecimal(LaborCostTextBox.Text, 0m),
                    PartsCost = ParseDecimal(PartsCostTextBox.Text, 0m),
                    LaborCost = ParseDecimal(LaborCostTextBox.Text, 0m),
                    ListingPrice = ParseDecimal(ListingPriceTextBox.Text, 0m),
                    SalePrice = ParseDecimal(SalePriceTextBox.Text, 0m),
                    RepairNotes = RepairNotesTextBox.Text.Trim(),
                    GeneralNotes = NotesTextBox.Text.Trim(),
                    BuyerFullName = BuyerNameTextBox.Text.Trim(),
                    BuyerPhone = BuyerPhoneTextBox.Text.Trim(),
                    BuyerPassport = BuyerPassportTextBox.Text.Trim()
                };
            }

            DialogResult = true;
            Close();
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(MakeTextBox.Text))
            {
                MessageBox.Show("Укажите марку автомобиля!", "Внимание");
                MakeTextBox.Focus();
                return false;
            }
            if (string.IsNullOrWhiteSpace(ModelTextBox.Text))
            {
                MessageBox.Show("Укажите модель автомобиля!", "Внимание");
                ModelTextBox.Focus();
                return false;
            }
            if (string.IsNullOrWhiteSpace(PlateTextBox.Text) && string.IsNullOrWhiteSpace(VinTextBox.Text))
            {
                MessageBox.Show("Укажите гос. номер или VIN — нужен хотя бы один идентификатор!", "Внимание");
                PlateTextBox.Focus();
                return false;
            }

            var year = ParseInt(YearTextBox.Text, DateTime.Now.Year);
            if (year < 1970 || year > DateTime.Now.Year + 1)
            {
                MessageBox.Show($"Год выпуска вне диапазона 1970–{DateTime.Now.Year + 1}!", "Внимание");
                YearTextBox.Focus();
                return false;
            }

            return true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ═════════════ МАРЖА (live-подсчёт) ═════════════

        private void Money_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdateMarginLabel();

        private void UpdateMarginLabel()
        {
            if (!_isEdit) return;
            var margin = ParseDecimal(SalePriceTextBox.Text, 0m)
                         - _original.PurchasePrice
                         - ParseDecimal(PartsCostTextBox.Text, 0m)
                         - ParseDecimal(LaborCostTextBox.Text, 0m);
            MarginLabel.Text = $"{margin:N0} ₽";
            MarginLabel.Foreground = margin >= 0
                ? (TryFindResource("AccentGreen") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Green)
                : (TryFindResource("AccentRed") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Red);
        }

        // ═════════════ ХЕЛПЕРЫ ВВОДА ═════════════

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
            e.Handled = !Regex.IsMatch(e.Text, @"^[0-9]+$");

        private void DecimalOnly_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
            e.Handled = !Regex.IsMatch(e.Text, @"^[0-9.,]+$");

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