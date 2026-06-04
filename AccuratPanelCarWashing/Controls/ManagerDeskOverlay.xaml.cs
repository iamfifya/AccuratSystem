using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Controls;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

using WpfUser = AccuratPanelCarWashing.Models.User;

namespace AccuratPanelCarWashing.Controls
{
    public partial class ManagerDeskOverlay : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private WpfUser _currentUser;

        public string ActiveUserInfo
        {
            get
            {
                if (_currentUser == null) return "Администратор неизвестен";
                return $"Авторизован: {_currentUser.FullName} ({_currentUser.RoleDisplay})";
            }
        }

        // ДОБАВЛЕНО: Свойство для отображения выручки
        private string _todayRevenueDisplay = "0";
        public string TodayRevenueDisplay
        {
            get => _todayRevenueDisplay;
            set
            {
                _todayRevenueDisplay = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TodayRevenueDisplay)));
            }
        }

        public ManagerDeskOverlay()
        {
            InitializeComponent();
            DataContext = this;
        }

        // ДОБАВЛЕНО: Принимаем currentShiftRevenue из Главного окна
        public void Show(WpfUser currentUser, decimal currentShiftRevenue)
        {
            _currentUser = currentUser;

            // Форматируем красиво: 50000 -> "50 000"
            TodayRevenueDisplay = currentShiftRevenue.ToString("N0");

            DataContext = null;
            DataContext = this;

            this.Visibility = Visibility.Visible;
            OverlayBackground.Visibility = Visibility.Visible;
            PopupPanel.Visibility = Visibility.Visible;

            var showAnimation = Resources["ShowAnimation"] as Storyboard;
            showAnimation?.Begin();
        }

        public void Hide()
        {
            var hideAnimation = Resources["HideAnimation"] as Storyboard;
            if (hideAnimation != null)
            {
                EventHandler onCompleted = null;
                onCompleted = (s, e) =>
                {
                    hideAnimation.Completed -= onCompleted;
                    this.Visibility = Visibility.Collapsed;
                    OverlayBackground.Visibility = Visibility.Collapsed;
                    PopupPanel.Visibility = Visibility.Collapsed;
                };
                hideAnimation.Completed += onCompleted;
                hideAnimation.Begin();
            }
            else
            {
                this.Visibility = Visibility.Collapsed;
                OverlayBackground.Visibility = Visibility.Collapsed;
                PopupPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();
        private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => Hide();
        private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

        private void EmployeesButton_Click(object sender, RoutedEventArgs e)
        {
            new EmployeeCardWindow().ShowDialog();
        }

        private void ServicesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!AccuratPanelCarWashing.Services.UserSession.IsFeatureEnabled(f => f.IsServicesEnabled))
            {
                MessageBox.Show("Модуль управления услугами отключен для этого филиала 🔒", "Доступ закрыт", MessageBoxButton.OK, MessageBoxImage.Information);
                // return; 
            }
            new ServiceManagementWindow().ShowDialog();
        }

        private void ClientsButton_Click(object sender, RoutedEventArgs e)
        {
            new ClientsWindow().ShowDialog();
        }

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!AccuratPanelCarWashing.Services.UserSession.IsFeatureEnabled(f => f.IsCrmMarketingEnabled))
            {
                MessageBox.Show("Расширенная аналитика недоступна для вашего филиала.\n\nСвяжитесь с поддержкой для активации 🔒", "Модуль заблокирован", MessageBoxButton.OK, MessageBoxImage.Warning);
                // return; 
            }
            new ReportsWindow(_currentUser).ShowDialog();
        }

        private void ReputationButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Модуль управления репутацией (Яндекс, Google, 2ГИС) находится в разработке.\n\nСкоро здесь будет статистика отзывов!", "Ждите обновлений", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BranchManagementBtn_Click(object sender, RoutedEventArgs e)
        {
            var window = new BranchManagementWindow();
            window.Owner = Window.GetWindow(this); // Чтобы окно центрировалось относительно главного
            window.ShowDialog();
        }

        // Событие, на которое подпишется Главное окно
        public event RoutedEventHandler CashboxRequested;

        /// <summary>
        /// Открывает панель управления кассой для текущей смены.
        /// </summary>
        private void CashboxButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Скрываем Стол Управляющего
            Hide();

            // 2. Сигнализируем Главному окну, что нужно открыть кассу
            CashboxRequested?.Invoke(this, e);
        }
    }
}