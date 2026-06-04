using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using AccuratSystem.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AccuratPanelCarWashing
{
    public partial class BranchManagementWindow : Window
    {
        private readonly ApiService _apiService = new ApiService();
        private List<Branch> _branches = new List<Branch>();
        private Branch _selectedBranch;
        private bool _isNewBranch = false;

        public BranchManagementWindow()
        {
            InitializeComponent();
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