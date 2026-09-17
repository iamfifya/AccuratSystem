using AccuratPanelCWD.Models;
using AccuratPanelCWD.Services;
using AccuratSystem.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AccuratPanelCWD
{
    public partial class BranchManagementWindow : Window
    {
        private readonly ApiService _apiService = new ApiService();
        private List<Branch> _branches = new List<Branch>();
        private Branch _selectedBranch;
        private bool _isNewBranch = false;
        public class TimeZoneOption
        {
            public string Id { get; set; }        // "Europe/Moscow" или "" для дефолта
            public string DisplayName { get; set; }
        }

        private static readonly List<TimeZoneOption> TimeZones = new List<TimeZoneOption>
            {
                new TimeZoneOption { Id = "",                  DisplayName = "По умолчанию (Europe/Moscow)" },
                new TimeZoneOption { Id = "Europe/Kaliningrad", DisplayName = "Калининград (UTC+2)" },
                new TimeZoneOption { Id = "Europe/Moscow",      DisplayName = "Москва (UTC+3)" },
                new TimeZoneOption { Id = "Europe/Samara",      DisplayName = "Самара (UTC+4)" },
                new TimeZoneOption { Id = "Asia/Yekaterinburg", DisplayName = "Екатеринбург (UTC+5)" },
                new TimeZoneOption { Id = "Asia/Omsk",          DisplayName = "Омск (UTC+6)" },
                new TimeZoneOption { Id = "Asia/Krasnoyarsk",   DisplayName = "Красноярск (UTC+7)" },
                new TimeZoneOption { Id = "Asia/Irkutsk",       DisplayName = "Иркутск (UTC+8)" },
                new TimeZoneOption { Id = "Asia/Yakutsk",       DisplayName = "Якутск (UTC+9)" },
                new TimeZoneOption { Id = "Asia/Vladivostok",   DisplayName = "Владивосток (UTC+10)" },
                new TimeZoneOption { Id = "Asia/Magadan",       DisplayName = "Магадан (UTC+11)" },
                new TimeZoneOption { Id = "Asia/Kamchatka",     DisplayName = "Камчатка (UTC+12)" },
            };

        public BranchManagementWindow()
        {
            InitializeComponent();
            TimeZoneComboBox.ItemsSource = TimeZones;
            LoadBranchesAsync();
        }

        private async Task LoadBranchesAsync()
        {
            try
            {
                this.IsEnabled = false;
                _branches = await _apiService.GetBranchesAsync();
                BranchesListBox.ItemsSource = null;
                BranchesListBox.ItemsSource = _branches;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке филиалов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.IsEnabled = true;
            }
        }

        private void BranchesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BranchesListBox.SelectedItem is Branch branch)
            {
                _selectedBranch = branch;
                _isNewBranch = false;

                // Заполняем форму
                EditTitleTextBlock.Text = $"Редактирование: {branch.Name}";
                NameTextBox.Text = branch.Name;
                AddressTextBox.Text = branch.Address;
                PhoneTextBox.Text = branch.Phone;
                WashBaysTextBox.Text = branch.WashBaysCount.ToString();
                ServiceLiftsTextBox.Text = branch.ServiceLiftsCount.ToString();
                IsActiveCheckBox.IsChecked = branch.IsActive;
                // Часовая зона: пустая строка в БД = дефолт (Europe/Moscow)
                TimeZoneComboBox.SelectedValue = string.IsNullOrEmpty(branch.TimeZoneId) ? "" : (object)branch.TimeZoneId;

                DeleteButton.Visibility = Visibility.Visible;
                EditPanel.IsEnabled = true;
            }
        }

        private void AddNewBranch_Click(object sender, RoutedEventArgs e)
        {
            BranchesListBox.SelectedItem = null;
            _selectedBranch = new Branch
            {
                CompanyId = AppSettings.CurrentCompanyId, // Если хранишь ID компании в настройках
                IsActive = true
            };
            _isNewBranch = true;

            EditTitleTextBlock.Text = "Создание нового филиала";
            NameTextBox.Text = "";
            AddressTextBox.Text = "";
            PhoneTextBox.Text = "";
            WashBaysTextBox.Text = "0";
            ServiceLiftsTextBox.Text = "0";
            IsActiveCheckBox.IsChecked = true;
            TimeZoneComboBox.SelectedValue = ""; // По умолчанию

            DeleteButton.Visibility = Visibility.Collapsed;
            EditPanel.IsEnabled = true;
            NameTextBox.Focus();
        }

        private async void SaveBranch_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("Укажите название филиала!", "Внимание");
                return;
            }

            try
            {
                this.IsEnabled = false;

                _selectedBranch.Name = NameTextBox.Text.Trim();
                _selectedBranch.Address = AddressTextBox.Text?.Trim();
                _selectedBranch.Phone = PhoneTextBox.Text?.Trim();
                _selectedBranch.WashBaysCount = int.TryParse(WashBaysTextBox.Text, out int w) ? w : 0;
                _selectedBranch.ServiceLiftsCount = int.TryParse(ServiceLiftsTextBox.Text, out int s) ? s : 0;
                _selectedBranch.IsActive = IsActiveCheckBox.IsChecked ?? false;

                var selectedZone = TimeZoneComboBox.SelectedValue as string;
                _selectedBranch.TimeZoneId = string.IsNullOrEmpty(selectedZone) ? null : selectedZone;

                if (_isNewBranch)
                {
                    await _apiService.CreateBranchAsync(_selectedBranch);
                    MessageBox.Show("Новый филиал успешно создан!", "Успех");
                }
                else
                {
                    await _apiService.UpdateBranchAsync(_selectedBranch);
                    MessageBox.Show("Филиал успешно обновлен!", "Успех");
                }

                EditPanel.IsEnabled = false;
                await LoadBranchesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка");
            }
            finally
            {
                this.IsEnabled = true;
            }
        }

        private async void DeleteBranch_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBranch == null || _isNewBranch) return;

            var result = MessageBox.Show($"Вы уверены, что хотите удалить филиал '{_selectedBranch.Name}'?\nЭто действие нельзя отменить!\nПри удалении филиала могут возникнуть ошибки!",
                                         "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    this.IsEnabled = false;
                    await _apiService.DeleteBranchAsync(_selectedBranch.Id);

                    EditPanel.IsEnabled = false;
                    await LoadBranchesAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка");
                }
                finally
                {
                    this.IsEnabled = true;
                }
            }
        }

        // Запрет ввода букв в поля для чисел
        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}