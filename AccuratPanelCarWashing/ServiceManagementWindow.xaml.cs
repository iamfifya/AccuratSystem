using AccuratSystem.Contracts.Models;
using AccuratPanelCarWashing.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AccuratPanelCarWashing
{
    public partial class ServiceManagementWindow : Window
    {
        // Подключаем наш новый API
        private ApiService _apiService;

        private List<Service> _allServices;

        public ServiceManagementWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();

            // Запускаем асинхронную загрузку при открытии окна
            _ = LoadServicesAsync();
        }

        // АСИНХРОННЫЙ МЕТОД: тянет услуги с сервера, а не из локального файлика
        private async Task LoadServicesAsync()
        {
            try
            {
                // Показываем загрузку (опционально, но хороший тон)
                ServicesListBox.ItemsSource = null;

                _allServices = await _apiService.GetServicesAsync();

                ApplyFilter();
                System.Diagnostics.Debug.WriteLine($"Загружено услуг с сервера: {_allServices.Count}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки услуг с сервера:\n{ex.Message}", "Ошибка API", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilter()
        {
            if (_allServices == null) return;

            string searchText = SearchTextBox.Text.Trim().ToLower();

            IEnumerable<Service> filtered = _allServices;

            // Фильтр по тексту
            if (!string.IsNullOrEmpty(searchText))
            {
                filtered = filtered.Where(s =>
                    s.Name.ToLower().Contains(searchText) ||
                    (s.Description != null && s.Description.ToLower().Contains(searchText)));
            }

            // Фильтр по категории
            if (_selectedCategoryFilter.HasValue)
            {
                filtered = filtered.Where(s => s.ServiceCategory == _selectedCategoryFilter.Value);
            }

            ServicesListBox.ItemsSource = filtered;
            ServicesListBox.SelectedItem = null;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ServicesListBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            var listBoxItem = FindParent<ListBoxItem>(source);

            if (listBoxItem == null)
            {
                ServicesListBox.SelectedItem = null;
                e.Handled = true;
            }
        }

        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is T))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as T;
        }

        private Service GetSelectedService()
        {
            return ServicesListBox.SelectedItem as Service;
        }

        private void EditService(Service service)
        {
            if (service == null) return;

            // 2. УБРАЛИ передачу _SqliteDataService
            var editWin = new AddEditServiceWindow(service);
            if (editWin.ShowDialog() == true)
            {
                _ = LoadServicesAsync();
                MessageBox.Show("Услуга обновлена", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is Service service)
            {
                DeleteService(service);
            }
        }

        private async void DeleteService(Service service)
        {
            if (service == null) return;

            var result = MessageBox.Show($"Удалить услугу \"{service.Name}\"?\n\nЭто действие нельзя отменить.",
                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    this.IsEnabled = false; // Блокируем окно на время запроса

                    // СТУЧИМ В API
                    await _apiService.DeleteServiceAsync(service.Id);

                    // ОБНОВЛЯЕМ СПИСОК (он скачается заново с сервера)
                    await LoadServicesAsync();

                    MessageBox.Show("Услуга удалена из базы PostgreSQL", "Успешно");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка API");
                }
                finally
                {
                    this.IsEnabled = true;
                }
            }
        }

        private AccuratSystem.Contracts.Enums.ServiceCategory? _selectedCategoryFilter;

        private void CategoryFilterCombo_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (CategoryFilterCombo.SelectedItem is ComboBoxItem item)
            {
                if (item.Tag == null)
                    _selectedCategoryFilter = null;
                else if (int.TryParse(item.Tag.ToString(), out int cat))
                    _selectedCategoryFilter = (AccuratSystem.Contracts.Enums.ServiceCategory)cat;
            }
            ApplyFilter();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadServicesAsync();
            MessageBox.Show("Список услуг обновлен", "Обновление", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is Service service)
            {
                EditService(service);
            }
        }

        private void CopyNameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is Service service)
            {
                Clipboard.SetText(service.Name);
                MessageBox.Show($"Название услуги \"{service.Name}\" скопировано в буфер обмена", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            // 3. УБРАЛИ передачу _SqliteDataService
            var addWin = new AddEditServiceWindow(null);
            if (addWin.ShowDialog() == true)
            {
                _ = LoadServicesAsync();
                MessageBox.Show("Услуга добавлена", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            var service = GetSelectedService();
            if (service != null)
            {
                EditService(service);
            }
            else
            {
                MessageBox.Show("Выберите услугу для редактирования", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var service = GetSelectedService();
            if (service != null)
            {
                DeleteService(service);
            }
            else
            {
                MessageBox.Show("Выберите услугу для удаления", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CheckIntegrityButton_Click(object sender, RoutedEventArgs e)
        {
            if (_allServices == null) return;

            var duplicateIds = _allServices.GroupBy(s => s.Id).Where(g => g.Count() > 1).ToList();
            var duplicateNames = _allServices.GroupBy(s => s.Name).Where(g => g.Count() > 1).ToList();

            string message = $"Всего услуг: {_allServices.Count}\n\n";

            if (duplicateIds.Any())
            {
                message += $"⚠️ Найдены дубликаты по ID:\n";
                foreach (var group in duplicateIds)
                    message += $"  ID {group.Key}: {group.Count()} услуг\n";
                message += "\n";
            }

            if (duplicateNames.Any())
            {
                message += $"⚠️ Найдены дубликаты по названию:\n";
                foreach (var group in duplicateNames)
                    message += $"  \"{group.Key}\": {group.Count()} услуг\n";
            }

            if (!duplicateIds.Any() && !duplicateNames.Any())
            {
                message += "✅ Дубликатов не найдено. База на сервере в порядке!";
            }

            MessageBox.Show(message, "Проверка целостности", MessageBoxButton.OK,
                duplicateIds.Any() || duplicateNames.Any() ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ServicesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var service = GetSelectedService();
            if (service != null)
            {
                EditService(service);
                e.Handled = true;
            }
        }
    }
}
