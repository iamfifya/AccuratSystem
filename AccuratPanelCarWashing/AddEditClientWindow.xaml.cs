using AccuratSystem.Contracts.Models;
using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace AccuratPanelCarWashing
{
    public partial class AddEditClientWindow : Window
    {
        // Добавили readonly
        private readonly ApiService _apiService;
        private List<Order> _allOrders = new List<Order>();
        // Если _todayAppointments тоже просит, сделай так:
        private readonly List<Appointment> _todayAppointments = new List<Appointment>();
        public Client CurrentClient { get; set; }
        public string WindowTitle { get; set; }

        // 1. УБИРАЕМ SqliteDataService ИЗ КОНСТРУКТОРА
        public AddEditClientWindow(Client client)
        {
            InitializeComponent();
            _apiService = new ApiService();

            if (client == null)
            {
                CurrentClient = new Client();
                WindowTitle = "➕ Добавление клиента";
            }
            else
            {
                CurrentClient = new Client
                {
                    Id = client.Id,
                    FullName = client.FullName,
                    Phone = client.Phone,
                    CarModel = client.CarModel,
                    CarNumber = client.CarNumber,
                    Notes = client.Notes,
                    RegistrationDate = client.RegistrationDate,
                    TotalSpent = client.TotalSpent,
                    VisitsCount = client.VisitsCount,
                    LastVisitDate = client.LastVisitDate,
                    DefaultDiscountPercent = client.DefaultDiscountPercent
                };
                WindowTitle = "✏ Редактирование клиента";
            }

            DataContext = this;
        }

        // Ограничитель ввода
        private void DiscountPercentTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CurrentClient.FullName) || string.IsNullOrWhiteSpace(CurrentClient.Phone))
                {
                    MessageBox.Show("Заполните ФИО и Телефон", "Внимание");
                    return;
                }

                this.IsEnabled = false;

                // ВАЖНО: Фиксим дату перед отправкой, чтобы Postgres не ругался!
                CurrentClient.RegistrationDate = DateTime.SpecifyKind(CurrentClient.RegistrationDate, DateTimeKind.Utc);
                if (CurrentClient.LastVisitDate.HasValue)
                {
                    CurrentClient.LastVisitDate = DateTime.SpecifyKind(CurrentClient.LastVisitDate.Value, DateTimeKind.Utc);
                }

                if (CurrentClient.Id == 0)
                {
                    await _apiService.CreateClientAsync(CurrentClient);
                }
                else
                {
                    await _apiService.UpdateClientAsync(CurrentClient);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка API");
            }
            finally { this.IsEnabled = true; }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
