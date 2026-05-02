using LiveCharts;
using LiveCharts.Wpf;
using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace AccuratPanelCarWashing.Controls
{
    public partial class ClientDetailsOverlay : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private Client _selectedClient;
        public Client SelectedClient
        {
            get => _selectedClient;
            set { _selectedClient = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedClient))); }
        }

        public string CarInfo => $"🚗 {SelectedClient?.CarModel} ({SelectedClient?.CarNumber})";
        public SeriesCollection ChartSeries { get; set; }
        public string[] ChartLabels { get; set; }
        public Func<double, string> YFormatter { get; set; }
        public List<TopServiceItem> TopServices { get; set; }

        public ClientDetailsOverlay()
        {
            InitializeComponent();
            this.DataContext = this;
        }

        // Изменяем сигнатуру метода на async
        public async Task ShowClientAsync(Client client, ApiService api)
        {
            SelectedClient = client;
            await LoadAnalyticsAsync(api);

            this.Visibility = Visibility.Visible;
            OverlayBackground.Visibility = Visibility.Visible;
            PopupPanel.Visibility = Visibility.Visible;

            var sb = (Storyboard)FindResource("ShowAnimation");
            sb.Begin();

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CarInfo)));
        }

        private async Task LoadAnalyticsAsync(ApiService api)
        {
            // Тянем заказы и услуги с сервера
            var orders = (await api.GetOrdersByClientIdAsync(SelectedClient.Id))
                         .Where(o => o.Status == "Выполнен").ToList();
            var allServices = await api.GetServicesAsync();

            var serviceCounts = new Dictionary<int, int>();
            foreach (var o in orders)
            {
                foreach (var sid in o.ServiceIds)
                {
                    if (!serviceCounts.ContainsKey(sid)) serviceCounts[sid] = 0;
                    serviceCounts[sid]++;
                }
            }

            TopServices = serviceCounts.OrderByDescending(kv => kv.Value).Take(3)
                .Select(kv => new TopServiceItem
                {
                    ServiceName = allServices.FirstOrDefault(s => s.Id == kv.Key)?.Name ?? "Услуга",
                    Count = kv.Value
                }).ToList();

            var months = new List<string>();
            var values = new ChartValues<double>();
            for (int i = 5; i >= 0; i--)
            {
                var date = DateTime.Now.AddMonths(-i);
                months.Add(date.ToString("MMM yy"));
                var sum = orders.Where(o => o.Time.Year == date.Year && o.Time.Month == date.Month).Sum(o => o.FinalPrice);
                values.Add((double)sum);
            }

            ChartLabels = months.ToArray();
            ChartSeries = new SeriesCollection { new ColumnSeries { Values = values, Fill = System.Windows.Media.Brushes.DodgerBlue } };
            YFormatter = v => v.ToString("N0") + " ₽";

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TopServices)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChartSeries)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChartLabels)));
        }

        private async void Close_Click(object sender, RoutedEventArgs e)
        {
            // Запуск анимации скрытия
            var sb = (Storyboard)FindResource("HideAnimation");
            sb.Begin();

            // Ждем завершения анимации перед тем, как скрыть элементы полностью
            await Task.Delay(200);

            this.Visibility = Visibility.Collapsed;
            OverlayBackground.Visibility = Visibility.Collapsed;
            PopupPanel.Visibility = Visibility.Collapsed;
        }
    }

    public class TopServiceItem
    {
        public string ServiceName { get; set; }
        public int Count { get; set; }
    }
}
