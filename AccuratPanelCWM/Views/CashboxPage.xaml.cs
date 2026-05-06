using AccuratPanelCWM.Models;
using AccuratPanelCWM.Services;

namespace AccuratPanelCWM.Views;

public partial class CashboxPage : ContentPage
{
    private readonly ApiService _apiService;
    private readonly int _shiftId;
    private readonly List<int> _shiftEmployeeIds;

    public CashboxPage(Shift shift) // Передаем весь объект смены
    {
        InitializeComponent();
        _apiService = new ApiService();
        _shiftId = shift.Id;
        _shiftEmployeeIds = shift.EmployeeIds;

        TypePicker.SelectedIndex = 1; // Расход по умолчанию
        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            // 1. Грузим вообще всех из базы
            var allUsers = await _apiService.GetUsersAsync();

            // 2. Оставляем только активных
            var activeStaff = allUsers.Where(u => u.IsActive).ToList();

            EmployeePicker.ItemsSource = activeStaff;

            // 3. Обновляем ленту операций
            var list = await _apiService.GetTransactionsByShiftAsync(_shiftId);
            TransactionsCollectionView.ItemsSource = list;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }

    private void TypePicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        // Показываем выбор сотрудника только если выбран аванс
        EmployeeLayout.IsVisible = TypePicker.SelectedItem?.ToString() == "Аванс мойщику";
    }

    private async void Save_Clicked(object sender, EventArgs e)
    {
        if (!decimal.TryParse(AmountEntry.Text, out decimal amount) || amount <= 0)
        {
            await DisplayAlert("Внимание", "Введите корректную сумму", "OK");
            return;
        }

        string type = TypePicker.SelectedItem?.ToString();
        int? empId = null;
        string comment = CommentEntry.Text ?? "";

        if (type == "Аванс мойщику")
        {
            if (EmployeePicker.SelectedItem is User selectedEmp)
            {
                empId = selectedEmp.Id;
                comment = $"Аванс: {selectedEmp.FullName}. {comment}";
            }
            else
            {
                await DisplayAlert("Внимание", "Выберите сотрудника", "OK");
                return;
            }
        }

        try
        {
            ((Button)sender).IsEnabled = false;

            var transaction = new Transaction
            {
                BranchId = AppSettings.CurrentBranchId,
                ShiftId = _shiftId,
                EmployeeId = empId,
                Amount = amount,
                Type = type,
                // Убеждаемся, что не шлем null в Comment и Department[cite: 35]
                Comment = string.IsNullOrWhiteSpace(comment) ? "Без комментария" : comment.Trim(),
                DateTime = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc),
                Department = "General"
            };

            await _apiService.CreateTransactionAsync(transaction);

            // Очищаем форму и обновляем список
            AmountEntry.Text = "";
            CommentEntry.Text = "";
            await LoadDataAsync();

            await DisplayAlert("Успех", "Операция проведена", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", ex.Message, "OK");
        }
        finally
        {
            ((Button)sender).IsEnabled = true;
        }
    }

    private async void Cancel_Clicked(object sender, EventArgs e) => await Navigation.PopModalAsync();
}