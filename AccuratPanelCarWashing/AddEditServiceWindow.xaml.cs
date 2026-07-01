using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using AccuratPanelCarWashing.ViewModels; // Добавили для PriceEntryViewModel
using AccuratSystem.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // Добавили
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace AccuratPanelCarWashing
{
    public partial class AddEditServiceWindow : Window
    {
        private readonly ApiService _apiService;
        public Service CurrentService { get; set; }
        public string WindowTitle { get; set; }

        // ДОБАВЛЕНО: Список для динамического отображения цен
        public ObservableCollection<PriceEntryViewModel> PriceEntries { get; set; } = new ObservableCollection<PriceEntryViewModel>();

        public AddEditServiceWindow(Service service)
        {
            InitializeComponent();
            _apiService = new ApiService();

            if (service == null)
            {
                CurrentService = new Service
                {
                    Id = 0,
                    Name = "",
                    DurationMinutes = 30,
                    Description = "",
                    IsActive = true,
                    PriceByBodyType = new Dictionary<int, decimal>(),
                    CustomWagePercentage = null
                };
                WindowTitle = "➕ Добавление услуги (API)";
            }
            else
            {
                CurrentService = new Service
                {
                    Id = service.Id,
                    CompanyId = service.CompanyId,
                    ServiceCategory = service.ServiceCategory,
                    Name = service.Name,
                    DurationMinutes = service.DurationMinutes,
                    Description = service.Description,
                    IsActive = service.IsActive,
                    PriceByBodyType = new Dictionary<int, decimal>(service.PriceByBodyType),
                    CustomWagePercentage = service.CustomWagePercentage,
                    BasePriceHint = service.BasePriceHint,
                    HasFloatingPrice = service.HasFloatingPrice
                };
                WindowTitle = "✏ Редактирование услуги (API)";
            }

            DataContext = this;

            // Запускаем загрузку категорий и цен
            _ = InitializePricesAsync();

            CategoryComboBox.SelectedValue = (int)CurrentService.ServiceCategory;
        }

        // НОВЫЙ МЕТОД: Загружает категории из API и заполняет список цен
        private async Task InitializePricesAsync()
        {
            try
            {
                // Определяем ID филиала (из текущей сессии)
                int branchId = AppSettings.CurrentBranchId;
                var categories = await _apiService.GetCarCategoriesAsync(branchId);

                PriceEntries.Clear();
                foreach (var cat in categories.OrderBy(c => c.SortOrder))
                {
                    // Достаем цену из словаря услуги, если её нет - ставим 0
                    decimal price = 0;
                    CurrentService.PriceByBodyType.TryGetValue(cat.Id, out price);

                    PriceEntries.Add(new PriceEntryViewModel
                    {
                        CategoryId = cat.Id,
                        CategoryName = cat.Name,
                        Price = price
                    });
                }

                // Привязываем список к UI-элементу
                PriceCategoriesControl.ItemsSource = PriceEntries;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки категорий кузова: {ex.Message}");
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CurrentService.Name))
                {
                    MessageBox.Show("Введите название услуги", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (CurrentService.DurationMinutes <= 0)
                {
                    MessageBox.Show("Введите корректную длительность", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // --- ИСПРАВЛЕНО: Собираем цены из динамического списка ---
                CurrentService.PriceByBodyType.Clear();
                foreach (var entry in PriceEntries)
                {
                    CurrentService.PriceByBodyType[entry.CategoryId] = entry.Price;
                }

                this.IsEnabled = false;

                if (CurrentService.Id == 0)
                {
                    await _apiService.CreateServiceAsync(CurrentService);
                }
                else
                {
                    await _apiService.UpdateServiceAsync(CurrentService);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении на сервере: {ex.Message}", "Ошибка API",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.IsEnabled = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
