using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using AccuratSystem.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WpfUser = AccuratPanelCarWashing.Models.User;

namespace AccuratPanelCarWashing
{
    public partial class SuperAdminWindow : Window
    {
        private readonly ApiService _apiService;
        private string _currentTableName = "";

        public SuperAdminWindow(WpfUser developerUser)
        {
            InitializeComponent();
            _apiService = new ApiService();
        }

        private async void TablesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TablesListBox.SelectedItem is ListBoxItem selectedItem)
            {
                _currentTableName = selectedItem.Tag.ToString();
                CurrentTableTitle.Text = $"Таблица: {_currentTableName}";

                await LoadTableDataAsync(_currentTableName);
            }
        }

        private async Task LoadTableDataAsync(string tableName)
        {
            DynamicDataGrid.ItemsSource = null;
            DynamicDataGrid.IsEnabled = false;

            try
            {
                switch (tableName)
                {
                    case "Companies": DynamicDataGrid.ItemsSource = await _apiService.GetCompaniesAsync(); break;
                    case "Branches": DynamicDataGrid.ItemsSource = await _apiService.GetBranchesAsync(); break;
                    case "Users": DynamicDataGrid.ItemsSource = await _apiService.GetUsersAsync(); break;
                    case "Services": DynamicDataGrid.ItemsSource = await _apiService.GetServicesAsync(); break;
                    case "Clients": DynamicDataGrid.ItemsSource = await _apiService.GetClientsAsync(); break;
                    case "Roles": DynamicDataGrid.ItemsSource = await _apiService.GetRolesAsync(); break;
                    case "TenantFeatures": DynamicDataGrid.ItemsSource = await _apiService.GetTenantFeaturesAsync(); break;
                    case "Orders": DynamicDataGrid.ItemsSource = await _apiService.GetOrdersAsync(); break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки таблицы {tableName}: {ex.Message}", "Ошибка");
            }
            finally
            {
                DynamicDataGrid.IsEnabled = true;
            }
        }

        private async void RefreshTable_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentTableName))
                await LoadTableDataAsync(_currentTableName);
        }

        // УНИВЕРСАЛЬНОЕ УДАЛЕНИЕ ИЗ БАЗЫ
        private async void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = DynamicDataGrid.SelectedItem;

            if (selectedItem == null)
            {
                MessageBox.Show("Выберите строку для удаления.", "Внимание");
                return;
            }

            if (MessageBox.Show("Вы уверены, что хотите удалить эту запись ИЗ БАЗЫ ДАННЫХ навсегда?", "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                this.IsEnabled = false;

                // Извлекаем ID с помощью паттерн-матчинга
                int idToDelete = 0;
                switch (_currentTableName)
                {
                    case "Companies": idToDelete = ((Company)selectedItem).Id; break;
                    case "Branches": idToDelete = ((Branch)selectedItem).Id; break;
                    case "Users": idToDelete = ((AccuratSystem.Contracts.Models.User)selectedItem).Id; break;
                    case "Services": idToDelete = ((Service)selectedItem).Id; break;
                    case "Clients": idToDelete = ((Client)selectedItem).Id; break;
                    case "Roles": idToDelete = ((Role)selectedItem).Id; break;
                    case "Orders": idToDelete = ((Order)selectedItem).Id; break;
                    case "TenantFeatures": idToDelete = ((TenantFeature)selectedItem).Id; break;
                }

                if (idToDelete == 0)
                {
                    MessageBox.Show("Этой записи еще нет в базе (или нельзя удалить).", "Внимание");
                    return;
                }

                switch (_currentTableName)
                {
                    case "Companies": await _apiService.DeleteCompanyAsync(idToDelete); break;
                    case "Branches": await _apiService.DeleteBranchAsync(idToDelete); break;
                    case "Roles": await _apiService.DeleteRoleAsync(idToDelete); break;
                    case "Users": await _apiService.DeleteUserAsync(idToDelete); break;
                    case "Services": await _apiService.DeleteServiceAsync(idToDelete); break;
                    case "Clients": await _apiService.DeleteClientAsync(idToDelete); break;
                    case "Orders": await _apiService.DeleteOrderAsync(idToDelete); break;
                    case "TenantFeatures": await _apiService.DeleteTenantFeatureAsync(idToDelete); break;
                }

                await LoadTableDataAsync(_currentTableName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.IsEnabled = true;
            }
        }

        // УНИВЕРСАЛЬНОЕ СОХРАНЕНИЕ (Дописано)
        private async void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = DynamicDataGrid.SelectedItem;

            if (selectedItem == null)
            {
                MessageBox.Show("Пожалуйста, выделите строку в таблице, которую хотите сохранить или добавить.", "Внимание");
                return;
            }

            try
            {
                this.IsEnabled = false;

                switch (_currentTableName)
                {
                    case "Companies":
                        var company = (Company)selectedItem;
                        if (company.Id == 0) await _apiService.CreateCompanyAsync(company);
                        else await _apiService.UpdateCompanyAsync(company);
                        break;

                    case "Roles":
                        var role = (Role)selectedItem;
                        if (role.Id == 0) await _apiService.CreateRoleAsync(role);
                        else await _apiService.UpdateRoleAsync(role);
                        break;

                    case "TenantFeatures":
                        var feature = (TenantFeature)selectedItem;
                        await _apiService.UpdateTenantFeatureAsync(feature);
                        break;

                    case "Users":
                        var user = (AccuratSystem.Contracts.Models.User)selectedItem;
                        if (user.Id == 0) await _apiService.CreateUserAsync(user);
                        else await _apiService.UpdateUserAsync(user);
                        break;

                    case "Branches":
                        var branch = (Branch)selectedItem;
                        if (branch.Id == 0) await _apiService.CreateBranchAsync(branch);
                        else await _apiService.UpdateBranchAsync(branch);
                        break;

                    case "Services":
                        var service = (Service)selectedItem;
                        if (service.Id == 0) await _apiService.CreateServiceAsync(service);
                        else await _apiService.UpdateServiceAsync(service);
                        break;

                    case "Clients":
                        var client = (Client)selectedItem;
                        if (client.Id == 0) await _apiService.CreateClientAsync(client);
                        else await _apiService.UpdateClientAsync(client);
                        break;

                    case "Orders":
                        var order = (Order)selectedItem;
                        if (order.Id == 0) await _apiService.CreateOrderAsync(order);
                        else await _apiService.UpdateOrderAsync(order);
                        break;

                    default:
                        MessageBox.Show("Сохранение для этой таблицы пока не реализовано.", "В разработке");
                        break;
                }

                MessageBox.Show("Изменения успешно сохранены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadTableDataAsync(_currentTableName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.IsEnabled = true;
            }
        }
    }
}