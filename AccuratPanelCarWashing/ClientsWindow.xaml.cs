using AccuratSystem.Contracts.Models;
using AccuratPanelCarWashing.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AccuratPanelCarWashing
{
    public partial class ClientsWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ApiService _apiService;
        private List<Client> _allClients;
        private Client _selectedClient;

        public ClientsWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            DataContext = this;

            ClientsListBox.LostFocus += (s, e) =>
            {
                var focusedElement = FocusManager.GetFocusedElement(this) as FrameworkElement;
                bool isControlButton = focusedElement is Button &&
                    (focusedElement.Name == "EditClientButton" ||
                     focusedElement.Name == "ShowStatsButton");

                if (!isControlButton)
                {
                    ClientsListBox.SelectedItem = null;
                    _selectedClient = null;
                }
            };

            _ = LoadClientsAsync();
        }

        private async Task LoadClientsAsync()
        {
            try
            {
                _allClients = await _apiService.GetClientsAsync();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки клиентов: {ex.Message}");
            }
        }

        private void ApplyFilter()
        {
            if (_allClients == null) return;
            string searchText = SearchTextBox.Text.Trim();

            var filtered = string.IsNullOrEmpty(searchText)
                ? _allClients
                : _allClients.Where(c =>
                    (c.FullName?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (c.Phone?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (c.CarNumber?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();

            ClientsListBox.ItemsSource = filtered;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
        private void RefreshButton_Click(object sender, RoutedEventArgs e) => _ = LoadClientsAsync();

        private void AddClient_Click(object sender, RoutedEventArgs e)
        {
            // 2. ИЗМЕНИЛИ вызов
            var addWin = new AddEditClientWindow(null);
            if (addWin.ShowDialog() == true) _ = LoadClientsAsync();
        }

        private void EditClient_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClient != null)
            {
                OpenEditClient(_selectedClient);
            }
            else
            {
                MessageBox.Show("Выберите клиента для редактирования", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenEditClient(Client client)
        {
            // 3. ИЗМЕНИЛИ вызов
            var editWin = new AddEditClientWindow(client);
            if (editWin.ShowDialog() == true) _ = LoadClientsAsync();
        }

        // Обработчик для кнопки в интерфейсе
        private void ShowStatsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClient != null)
            {
                ShowClientStats(_selectedClient);
            }
            else
            {
                MessageBox.Show("Выберите клиента для просмотра статистики", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        // Обработчик для пункта контекстного меню (на него ругается компилятор)
        private void ShowStatsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClient != null)
            {
                ShowClientStats(_selectedClient);
            }
            else
            {
                MessageBox.Show("Выберите клиента для просмотра статистики", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        // Общий метод для запуска асинхронного показа статистики
        private async void ShowClientStats(Client client)
        {
            if (client != null)
            {
                try
                {
                    // Вызываем асинхронный метод оверлея, который мы подготовили
                    await DetailsOverlay.ShowClientAsync(client, _apiService);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки статистики: {ex.Message}", "Ошибка API");
                }
            }
        }
        // И на всякий случай добавь этот, если он тоже потерялся:
        private void EditClientMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClient != null)
            {
                OpenEditClient(_selectedClient);
            }
        }

        private void ClientsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Двойной клик - открываем редактирование
            if (_selectedClient != null)
            {
                OpenEditClient(_selectedClient);
                e.Handled = true;
            }
        }

        private void ClientsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedClient = ClientsListBox.SelectedItem as Client;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
