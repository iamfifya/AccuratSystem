using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using AccuratSystem.Contracts.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace AccuratPanelCarWashing.Controls
{
    public partial class CompanyManagementOverlay : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly ApiService _apiService = new ApiService();

        public CompanyManagementOverlay()
        {
            InitializeComponent();
            this.DataContext = this;
        }

        public async void Show()
        {
            this.Visibility = Visibility.Visible;
            OverlayBackground.Visibility = Visibility.Visible;
            PopupPanel.Visibility = Visibility.Visible;

            await LoadAllDataAsync();

            var sb = (Storyboard)this.FindResource("ShowAnimation");
            sb?.Begin();
        }

        public void Hide()
        {
            var sb = (Storyboard)this.FindResource("HideAnimation");
            if (sb != null)
            {
                EventHandler completed = null;
                completed = (s, e) => {
                    this.Visibility = Visibility.Collapsed;
                    OverlayBackground.Visibility = Visibility.Collapsed;
                    PopupPanel.Visibility = Visibility.Collapsed;
                };
                sb.Completed += completed;
                sb.Begin();
            }
            else
            {
                this.Visibility = Visibility.Collapsed;
            }
        }

        private async Task LoadAllDataAsync()
        {
            try
            {
                int branchId = AppSettings.CurrentBranchId;

                var cats = await _apiService.GetCarCategoriesAsync(branchId);
                CategoriesList.ItemsSource = cats;

                var payments = await _apiService.GetPaymentMethodsAsync(branchId); // Исправлено
                PaymentsList.ItemsSource = payments;

                var roles = await _apiService.GetRolesAsync();
                RolesList.ItemsSource = roles;

                var settings = await _apiService.GetCompanySettingsAsync(branchId);
                if (settings != null)
                {
                    SettingCompanyShare.Text = settings.CompanySharePercentage.ToString();
                    SettingAdminBase.Text = "0";
                    SettingAdminPercent.Text = settings.DayShiftAdminPercentage.ToString();
                    SettingNightPercent.Text = settings.NightShiftWasherPercentage.ToString();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        #region Категории

        private void CategoriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoriesList.SelectedItem is CarCategory cat)
            {
                CatNameText.Text = cat.Name;
                CatOrderText.Text = cat.SortOrder.ToString();
            }
            else
            {
                // Очищаем поля, если ничего не выбрано (режим создания)
                CatNameText.Text = "";
                CatOrderText.Text = "0";
            }
        }

        private async void SaveCategory_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CatNameText.Text))
            {
                MessageBox.Show("Введите название категории!", "Внимание");
                return;
            }

            try
            {
                this.IsEnabled = false;

                if (CategoriesList.SelectedItem is CarCategory selectedCat)
                {
                    // РЕЖИМ РЕДАКТИРОВАНИЯ
                    selectedCat.Name = CatNameText.Text;
                    if (int.TryParse(CatOrderText.Text, out int order)) selectedCat.SortOrder = order;

                    await _apiService.UpdateCategoryAsync(selectedCat);
                    MessageBox.Show("Категория обновлена!");
                }
                else
                {
                    // РЕЖИМ СОЗДАНИЯ
                    var newCat = new CarCategory
                    {
                        Name = CatNameText.Text,
                        SortOrder = int.TryParse(CatOrderText.Text, out int order) ? order : 0
                    };

                    await _apiService.CreateCategoryAsync(newCat);
                    MessageBox.Show("Новая категория создана!");
                }

                await LoadAllDataAsync(); // Обновляем список
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
            finally
            {
                this.IsEnabled = true;
            }
        }

        private async void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (CategoriesList.SelectedItem is CarCategory cat)
            {
                var result = MessageBox.Show($"Вы уверены, что хотите удалить категорию '{cat.Name}'?\nЭто может повлиять на отображение цен в услугах!",
                                             "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        this.IsEnabled = false;
                        await _apiService.DeleteCategoryAsync(cat.Id);
                        await LoadAllDataAsync();
                        MessageBox.Show("Категория удалена");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении: {ex.Message}");
                    }
                    finally
                    {
                        this.IsEnabled = true;
                    }
                }
            }
        }
        #endregion


        private void PaymentsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PaymentsList.SelectedItem is PaymentMethod pm)
            {
                PayNameText.Text = pm.Name;
                PayActiveCheck.IsChecked = pm.IsActive;
                PayOrderText.Text = pm.SortOrder.ToString();
            }
        }

        private async void SavePayment_Click(object sender, RoutedEventArgs e)
        {
            if (PaymentsList.SelectedItem is PaymentMethod pm)
            {
                pm.Name = PayNameText.Text;
                pm.IsActive = PayActiveCheck.IsChecked ?? true;
                if (int.TryParse(PayOrderText.Text, out int order)) pm.SortOrder = order;
                MessageBox.Show("Обновлено!");
            }
        }

        private void DeletePayment_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Удаление оплаты...");

        private void RolesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RolesList.SelectedItem is Role role) RoleNameText.Text = role.Name;
        }

        private async void SaveRole_Click(object sender, RoutedEventArgs e)
        {
            if (RolesList.SelectedItem is Role role)
            {
                role.Name = RoleNameText.Text;
                await _apiService.UpdateRoleAsync(role);
                await LoadAllDataAsync();
            }
        }

        private void DeleteRole_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Удаление роли...");

        private async void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Настройки сохранены!");
        }

        private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => Hide();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
