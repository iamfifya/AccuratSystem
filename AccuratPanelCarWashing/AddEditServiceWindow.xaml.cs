using AccuratSystem.Contracts.Models;
using AccuratPanelCarWashing.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace AccuratPanelCarWashing
{
    public partial class AddEditServiceWindow : Window
    {
        private readonly ApiService _apiService;
        public Service CurrentService { get; set; }
        public string WindowTitle { get; set; }

        public AddEditServiceWindow(Service service)
        {
            InitializeComponent();

            // Инициализируем наш новый сервис API
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
                    CustomWagePercentage = null // Явно ставим null для новой услуги
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
                    BasePriceHint = service.BasePriceHint,     // На всякий случай
                    HasFloatingPrice = service.HasFloatingPrice // На всякий случай
                };
                WindowTitle = "✏ Редактирование услуги (API)";
            }

            DataContext = this;
            LoadPricesToUI();

            // Теперь здесь всегда будет правильное значение, а не дефолтный 0!
            CategoryComboBox.SelectedValue = (int)CurrentService.ServiceCategory;
        }

        private void LoadPricesToUI()
        {
            if (CurrentService.PriceByBodyType.TryGetValue(1, out var p1))
                PriceCategory1TextBox.Text = p1.ToString();
            if (CurrentService.PriceByBodyType.TryGetValue(2, out var p2))
                PriceCategory2TextBox.Text = p2.ToString();
            if (CurrentService.PriceByBodyType.TryGetValue(3, out var p3))
                PriceCategory3TextBox.Text = p3.ToString();
            if (CurrentService.PriceByBodyType.TryGetValue(4, out var p4))
                PriceCategory4TextBox.Text = p4.ToString();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Валидация
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

                // Собираем цены из текстовых полей
                CurrentService.PriceByBodyType.Clear();
                decimal parsePrice(string text) => decimal.TryParse(text, out var p) && p >= 0 ? p : 0;

                CurrentService.PriceByBodyType[1] = parsePrice(PriceCategory1TextBox.Text);
                CurrentService.PriceByBodyType[2] = parsePrice(PriceCategory2TextBox.Text);
                CurrentService.PriceByBodyType[3] = parsePrice(PriceCategory3TextBox.Text);
                CurrentService.PriceByBodyType[4] = parsePrice(PriceCategory4TextBox.Text);

                // Визуальная индикация работы (можно добавить ProgressBar, если захочешь)
                this.IsEnabled = false;

                if (CurrentService.Id == 0)
                {
                    // === ОТПРАВКА НА СЕРВЕР (НОВАЯ) ===
                    await _apiService.CreateServiceAsync(CurrentService);
                    System.Diagnostics.Debug.WriteLine($"Услуга создана через API: {CurrentService.Name}");
                }
                else
                {
                    // === ОТПРАВКА НА СЕРВЕР (ОБНОВЛЕНИЕ) ===
                    await _apiService.UpdateServiceAsync(CurrentService);
                    System.Diagnostics.Debug.WriteLine($"Услуга обновлена через API: {CurrentService.Name}");
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
