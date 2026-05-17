using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using AccuratPanelCWM.Models;
using AccuratPanelCWM.Services;

namespace AccuratPanelCWM.Views
{
    public partial class OrdersPage : ContentPage
    {
        private readonly ApiService _apiService;
        private List<Branch> _cachedBranches = new List<Branch>();

        // Используем твой BranchTabItem для Picker
        public ObservableCollection<BranchTabItem> Branches { get; set; } = new ObservableCollection<BranchTabItem>();
        public ObservableCollection<BoxDisplayModel> Boxes { get; set; } = new ObservableCollection<BoxDisplayModel>();

        public OrdersPage()
        {
            InitializeComponent();
            _apiService = new ApiService();

            BranchPicker.ItemsSource = Branches;
            BoxesCollectionView.ItemsSource = Boxes;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (Branches.Count == 0)
            {
                await LoadBranchesAsync();
            }
            else if (BranchPicker.SelectedItem is BranchTabItem selected)
            {
                // Если филиалы уже загружены, просто обновляем боксы
                await LoadBoxesAsync(selected.BranchId);
            }
        }

        private async Task LoadBranchesAsync()
        {
            try
            {
                _cachedBranches = await _apiService.GetBranchesAsync();

                Branches.Clear();
                foreach (var branch in _cachedBranches)
                {
                    Branches.Add(new BranchTabItem { BranchId = branch.Id, BranchName = branch.Name });
                }

                if (Branches.Count > 0)
                    BranchPicker.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка", $"Не удалось загрузить филиалы: {ex.Message}", "ОК");
            }
        }

        private async void BranchPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (BranchPicker.SelectedItem is BranchTabItem selectedBranch)
            {
                await LoadBoxesAsync(selectedBranch.BranchId);
            }
        }

        private async void RefreshView_Refreshing(object sender, EventArgs e)
        {
            if (BranchPicker.SelectedItem is BranchTabItem selectedBranch)
            {
                await LoadBoxesAsync(selectedBranch.BranchId);
            }
            else
            {
                OrdersRefreshView.IsRefreshing = false;
            }
        }

        // --- ГЕНЕРАЦИЯ И ЗАПОЛНЕНИЕ БОКСОВ ---
        // Предохранитель от двойного запуска из-за бага RefreshView в MAUI
        private bool _isLoadingBoxes = false;

        private async Task LoadBoxesAsync(int branchId)
        {
            // Если уже грузим - выходим, чтобы не было дублей
            if (_isLoadingBoxes) return;
            _isLoadingBoxes = true;

            // Включаем крутилку (если вызвано не свайпом)
            MainThread.BeginInvokeOnMainThread(() => OrdersRefreshView.IsRefreshing = true);

            try
            {
                var currentBranch = _cachedBranches.FirstOrDefault(b => b.Id == branchId);
                if (currentBranch == null) return;

                var activeOrders = await _apiService.GetActiveOrdersAsync(branchId);

                // Подготавливаем новый список в фоне
                var newBoxes = new List<BoxDisplayModel>();

                // 1. Генерируем боксы МОЙКИ (если они есть)
                for (int i = 1; i <= currentBranch.WashBaysCount; i++)
                {
                    var orderInBox = activeOrders.FirstOrDefault(o => o.BoxNumber == i && o.Department == "Wash");

                    newBoxes.Add(new BoxDisplayModel
                    {
                        BoxNumber = i,
                        Department = "Wash",
                        BoxName = $"🚿 Бокс №{i} (Мойка)",
                        CurrentOrder = orderInBox
                    });
                }

                // 2. Генерируем ПОДЪЕМНИКИ СЕРВИСА (если они есть)
                for (int i = 1; i <= currentBranch.ServiceLiftsCount; i++)
                {
                    var orderInBox = activeOrders.FirstOrDefault(o => o.BoxNumber == i && o.Department == "Service");

                    newBoxes.Add(new BoxDisplayModel
                    {
                        BoxNumber = i,
                        Department = "Service",
                        BoxName = $"🔧 Подъёмник №{i} (Сервис)",
                        CurrentOrder = orderInBox
                    });
                }

                // Безопасно обновляем UI-коллекцию в главном потоке
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Boxes.Clear();
                    foreach (var box in newBoxes)
                    {
                        Boxes.Add(box);
                    }
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка", $"Не удалось загрузить зоны: {ex.Message}", "ОК");
            }
            finally
            {
                // Выключаем крутилку и снимаем блокировку
                MainThread.BeginInvokeOnMainThread(() => OrdersRefreshView.IsRefreshing = false);
                _isLoadingBoxes = false;
            }
        }

        // ДОБАВИТЬ ЗАКАЗ
        private async void AddOrderButton_Clicked(object sender, EventArgs e)
        {
            var btn = (Button)sender;
            var box = (BoxDisplayModel)btn.CommandParameter;

            if (BranchPicker.SelectedItem is BranchTabItem selectedBranch)
            {
                // Переходим на страницу создания заказа, передавая нужные параметры
                await Navigation.PushAsync(new AddOrderPage(
                    selectedBranch.BranchId,
                    box.BoxNumber,
                    box.Department,
                    box.BoxName));
            }
        }

        // ЗАВЕРШИТЬ ЗАКАЗ (С ВЫБОРОМ ОПЛАТЫ)
        private async void FinishOrderButton_Clicked(object sender, EventArgs e)
        {
            var btn = (Button)sender;
            var box = (BoxDisplayModel)btn.CommandParameter;

            if (box.CurrentOrder == null) return;

            // 1. Спрашиваем способ оплаты с помощью красивого меню
            string paymentMethod = await DisplayActionSheet(
                $"Оплата: {box.CurrentOrder.CarNumber}",
                "Отмена",
                null,
                "💵 Наличные", "💳 Карта", "📱 Перевод", "📱 QR-код");

            // Если нажали отмену или мимо меню
            if (paymentMethod == "Отмена" || string.IsNullOrEmpty(paymentMethod)) return;

            // Очищаем смайлики из строки для базы данных
            string cleanPaymentMethod = paymentMethod.Replace("💵 ", "").Replace("💳 ", "").Replace("📱 ", "");

            // 2. Отправляем запрос с выбранным способом
            bool success = await _apiService.CompleteOrderAsync(box.CurrentOrder.Id, cleanPaymentMethod);

            if (success)
            {
                await LoadBoxesAsync(box.CurrentOrder.BranchId); // Перезагружаем боксы
            }
            else
            {
                // Если всё равно ошибка, значит проблема с сетью
                await DisplayAlert("Ошибка", "Не удалось завершить заказ. Проверьте подключение к серверу.", "ОК");
            }
        }
    }
}