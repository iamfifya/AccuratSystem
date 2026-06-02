using AccuratPanelCWM.Services;
using AccuratSystem.Contracts.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;

namespace AccuratPanelCWM.ViewModels
{
    // Обертка для красивого вывода транзакции (иконки, цвета)
    public class TransactionUiWrapper
    {
        public Transaction BaseTrans { get; set; }
        public string Icon => BaseTrans.Type switch
        {
            "Приход" => "💵",
            "Расход" => "🛒",
            "Аванс мойщику" => "👤",
            "Инкассация" => "🏦",
            "Размен" => "🪙",
            _ => "📄"
        };
        public string ColorHex => (BaseTrans.Type == "Приход" || BaseTrans.Type == "Размен") ? "#27AE60" : "#E74C3C";
        public string FormattedAmount => (BaseTrans.Type == "Приход" || BaseTrans.Type == "Размен") ? $"+{BaseTrans.Amount:N0} ₽" : $"-{BaseTrans.Amount:N0} ₽";
    }

    public partial class CashboxViewModel : ObservableObject
    {
        private readonly ApiService _apiService;
        private int _shiftId;

        [ObservableProperty] private bool _isBusy;

        [ObservableProperty] private string _selectedType;
        [ObservableProperty] private bool _isEmployeeVisible;

        [ObservableProperty] private string _amountText;
        [ObservableProperty] private string _commentText;

        [ObservableProperty] private User _selectedEmployee;

        public List<string> TransactionTypes { get; } = new() { "Приход", "Расход", "Аванс мойщику", "Инкассация", "Размен" };
        public ObservableCollection<User> Employees { get; } = new();
        public ObservableCollection<TransactionUiWrapper> Transactions { get; } = new();

        public CashboxViewModel(ApiService apiService)
        {
            _apiService = apiService;
            SelectedType = "Расход"; // Дефолтное значение
        }

        public void Initialize(int shiftId)
        {
            _shiftId = shiftId;
            LoadDataCommand.Execute(null);
        }

        // 💥 Магия MVVM: Срабатывает автоматически при смене типа в Picker
        partial void OnSelectedTypeChanged(string value)
        {
            IsEmployeeVisible = value == "Аванс мойщику";
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            IsBusy = true;
            try
            {
                // Запускаем запросы параллельно для скорости
                var usersTask = _apiService.GetUsersAsync();
                var transactionsTask = _apiService.GetTransactionsByShiftAsync(_shiftId);

                await Task.WhenAll(usersTask, transactionsTask);

                Employees.Clear();
                foreach (var u in usersTask.Result.Where(u => u.IsActive))
                {
                    Employees.Add(u);
                }

                Transactions.Clear();
                // Сортируем так, чтобы новые операции были сверху
                foreach (var t in transactionsTask.Result.OrderByDescending(x => x.DateTime))
                {
                    Transactions.Add(new TransactionUiWrapper { BaseTrans = t });
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", ex.Message, "OK");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task SaveTransactionAsync()
        {
            if (!decimal.TryParse(AmountText?.Replace(".", ","), out decimal amount) || amount <= 0)
            {
                await Application.Current.MainPage.DisplayAlert("Внимание", "Введите корректную сумму", "OK");
                return;
            }

            if (string.IsNullOrEmpty(SelectedType))
            {
                await Application.Current.MainPage.DisplayAlert("Внимание", "Выберите тип операции", "OK");
                return;
            }

            string comment = CommentText ?? "";
            int? empId = null;

            if (SelectedType == "Аванс мойщику")
            {
                if (SelectedEmployee == null)
                {
                    await Application.Current.MainPage.DisplayAlert("Внимание", "Выберите сотрудника", "OK");
                    return;
                }
                empId = SelectedEmployee.Id;
                comment = $"Аванс: {SelectedEmployee.FullName}. {comment}";
            }

            IsBusy = true;
            try
            {
                var transaction = new Transaction
                {
                    BranchId = Preferences.Default.Get("CurrentBranchId", 0),
                    ShiftId = _shiftId,
                    EmployeeId = empId,
                    Amount = amount,
                    Type = SelectedType,
                    Comment = string.IsNullOrWhiteSpace(comment) ? "Без комментария" : comment.Trim(),
                    DateTime = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc),
                    Department = "General"
                };

                await _apiService.CreateTransactionAsync(transaction);

                AmountText = "";
                CommentText = "";
                SelectedEmployee = null;

                await LoadDataAsync(); // Обновляем ленту операций
                await Application.Current.MainPage.DisplayAlert("Успех", "Операция проведена", "OK");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", ex.Message, "OK");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task CloseAsync()
        {
            await Application.Current.MainPage.Navigation.PopModalAsync();
        }
    }
}