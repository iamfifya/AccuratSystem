using AccuratPanelCWM.Services;
using AccuratPanelCWM.Views; // Для навигации на AddOrderPage
using AccuratSystem.Contracts.Models; // Общие контракты
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;

namespace AccuratPanelCWM.ViewModels
{
    // Воссоздаем нашу UI-модель для карточки бокса прямо здесь
    public partial class BoxDisplayModel : ObservableObject
    {
        public int BoxNumber { get; set; }
        public string Department { get; set; }
        public string BoxName { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsBusy))]
        [NotifyPropertyChangedFor(nameof(IsFree))]
        private Order _currentOrder;

        public bool IsBusy => CurrentOrder != null;
        public bool IsFree => CurrentOrder == null;
    }

    public partial class OrdersViewModel : ObservableObject
    {
        private readonly ApiService _apiService;
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty] private bool _isBusy;

        public ObservableCollection<Branch> Branches { get; } = new();
        public ObservableCollection<BoxDisplayModel> Boxes { get; } = new();

        [ObservableProperty]
        private Branch _selectedBranch;

        // МАГИЯ: Автоматически срабатывает, когда меняется SelectedBranch
        partial void OnSelectedBranchChanged(Branch value)
        {
            if (value != null)
            {
                Preferences.Default.Set("CurrentBranchId", value.Id);
                LoadBoxesCommand.Execute(null);
            }
        }

        public OrdersViewModel(ApiService apiService, IServiceProvider serviceProvider)
        {
            _apiService = apiService;
            _serviceProvider = serviceProvider; // Сохраняем провайдер
            LoadBranchesCommand.Execute(null);
        }

        [RelayCommand]
        private async Task LoadBranchesAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var branches = await _apiService.GetBranchesAsync();
                Branches.Clear();
                foreach (var b in branches) Branches.Add(b);

                // Восстанавливаем последний выбранный филиал
                var currentBranchId = Preferences.Default.Get("CurrentBranchId", 0);
                SelectedBranch = Branches.FirstOrDefault(b => b.Id == currentBranchId) ?? Branches.FirstOrDefault();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", $"Не удалось загрузить филиалы: {ex.Message}", "ОК");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task LoadBoxesAsync()
        {
            if (SelectedBranch == null) return;
            IsBusy = true;
            try
            {
                var activeOrders = await _apiService.GetActiveOrdersAsync(SelectedBranch.Id);
                var newBoxes = new List<BoxDisplayModel>();

                // 1. Мойка
                for (int i = 1; i <= SelectedBranch.WashBaysCount; i++)
                {
                    newBoxes.Add(new BoxDisplayModel
                    {
                        BoxNumber = i,
                        Department = "Wash",
                        BoxName = $"🚿 Бокс №{i} (Мойка)",
                        CurrentOrder = activeOrders.FirstOrDefault(o => o.BoxNumber == i && o.Department == "Wash")
                    });
                }

                // 2. Сервис
                for (int i = 1; i <= SelectedBranch.ServiceLiftsCount; i++)
                {
                    newBoxes.Add(new BoxDisplayModel
                    {
                        BoxNumber = i,
                        Department = "Service",
                        BoxName = $"🔧 Подъёмник №{i} (Сервис)",
                        CurrentOrder = activeOrders.FirstOrDefault(o => o.BoxNumber == i && o.Department == "Service")
                    });
                }

                Boxes.Clear();
                foreach (var box in newBoxes) Boxes.Add(box);
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", $"Не удалось загрузить зоны: {ex.Message}", "ОК");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task AddOrderAsync(BoxDisplayModel box)
        {
            if (SelectedBranch == null || box == null) return;

            // Просим DI-контейнер собрать нам страницу
            var addOrderPage = _serviceProvider.GetRequiredService<AddOrderPage>();

            // Достаем её ViewModel и инициализируем нашими данными
            var vm = (AddOrderViewModel)addOrderPage.BindingContext;
            vm.Initialize(SelectedBranch.Id, box.BoxNumber, box.Department, box.BoxName);

            await Application.Current.MainPage.Navigation.PushAsync(addOrderPage);
        }

        [RelayCommand]
        private async Task FinishOrderAsync(BoxDisplayModel box)
        {
            if (box?.CurrentOrder == null) return;

            string paymentMethod = await Application.Current.MainPage.DisplayActionSheet(
                $"Оплата: {box.CurrentOrder.CarNumber}",
                "Отмена",
                null,
                "💵 Наличные", "💳 Карта", "📱 Перевод", "📱 QR-код");

            if (paymentMethod == "Отмена" || string.IsNullOrEmpty(paymentMethod)) return;

            string cleanPaymentMethod = paymentMethod.Replace("💵 ", "").Replace("💳 ", "").Replace("📱 ", "");

            IsBusy = true; // Крутим спиннер
            bool success = await _apiService.CompleteOrderAsync(box.CurrentOrder.Id, cleanPaymentMethod);

            if (success)
            {
                await LoadBoxesAsync(); // Сразу обновляем список
            }
            else
            {
                IsBusy = false;
                await Application.Current.MainPage.DisplayAlert("Ошибка", "Не удалось завершить заказ. Проверьте сеть.", "ОК");
            }
        }
    }
}