using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace AccuratPanelCarWashing.Controls
{
    public partial class CashboxOverlay : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly ApiService _apiService = new ApiService();
        private Shift _currentShift;

        public ObservableCollection<Transaction> Transactions { get; set; } = new ObservableCollection<Transaction>();
        public ObservableCollection<User> ActiveEmployees { get; set; } = new ObservableCollection<User>();

        private decimal _cashInHand, _totalExpenses, _netCashProfit;
        public decimal CashInHand { get => _cashInHand; set { _cashInHand = value; OnPropertyChanged(nameof(CashInHand)); } }
        public decimal TotalExpenses { get => _totalExpenses; set { _totalExpenses = value; OnPropertyChanged(nameof(TotalExpenses)); } }
        public decimal NetCashProfit { get => _netCashProfit; set { _netCashProfit = value; OnPropertyChanged(nameof(NetCashProfit)); } }

        public CashboxOverlay()
        {
            InitializeComponent();
            DataContext = this;
        }

        public void Show(Shift shift)
        {
            if (shift == null) { MessageBox.Show("Смена не открыта!"); return; }
            _currentShift = shift;

            _ = RefreshDataAsync(); // Асинхронно обновляем цифры

            this.Visibility = Visibility.Visible;
            OverlayBackground.Visibility = Visibility.Visible;
            PopupPanel.Visibility = Visibility.Visible;
            ((Storyboard)FindResource("ShowAnimation")).Begin();
        }

        private async Task RefreshDataAsync()
        {
            // 1. Загружаем сотрудников смены (просто для выпадающего списка "Кому аванс")
            ActiveEmployees.Clear();
            var allUsers = await _apiService.GetUsersAsync();
            foreach (var empId in _currentShift.EmployeeIds)
            {
                var user = allUsers.FirstOrDefault(u => u.Id == empId);
                if (user != null) ActiveEmployees.Add(user);
            }

            // 2. Получаем ленту операций из API
            var list = await _apiService.GetTransactionsByShiftAsync(_currentShift.Id);
            Transactions.Clear();
            foreach (var t in list) Transactions.Add(t);

            // 3. Получаем готовую математику из API! 🎉
            var summary = await _apiService.GetShiftCashboxSummaryAsync(_currentShift.Id);
            CashInHand = summary.CashInHand;
            TotalExpenses = summary.TotalExpenses;
            NetCashProfit = summary.NetCashProfit;
        }

        private async void AddTransaction_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(AmountText.Text.Replace(".", ","), out decimal amt) || amt <= 0)
            {
                MessageBox.Show("Введите корректную сумму", "Ошибка"); return;
            }

            string type = SelectedOperationType;
            if (string.IsNullOrEmpty(type)) { MessageBox.Show("Выберите тип операции", "Ошибка"); return; }

            string comment = string.IsNullOrWhiteSpace(CommentText.Text) ? "Без комментария" : CommentText.Text;
            int? empId = null;

            if (type == "Аванс мойщику")
            {
                if (SelectedEmployee == null) { MessageBox.Show("Выберите кому выдать аванс"); return; }
                empId = SelectedEmployee.Id;
                comment = $"Аванс: {SelectedEmployee.FullName}. {comment}";
            }

            // СБОРКА ОБЪЕКТА ДЛЯ СЕРВЕРА
            var newTransaction = new Transaction
            {
                BranchId = AppSettings.CurrentBranchId, // Важно!
                ShiftId = _currentShift.Id,
                EmployeeId = empId,
                Amount = amt,
                Type = type,
                Comment = comment,
                DateTime = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc) // Важно!
            };

            try
            {
                this.IsEnabled = false; // Блокируем окно, пока идет запрос

                await _apiService.CreateTransactionAsync(newTransaction);
                MessageBox.Show("Операция проведена!");

                AmountText.Clear(); CommentText.Clear();
                SelectedOperationType = null; SelectedEmployee = null;

                await RefreshDataAsync(); // Обновляем цифры после проведения
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось сохранить операцию: {ex.Message}");
            }
            finally { this.IsEnabled = true; }
        }

        // --- Инфраструктурный код для ComboBox ---
        private string _selectedOpType;
        public string SelectedOperationType
        {
            get => _selectedOpType;
            set { _selectedOpType = value; OnPropertyChanged(nameof(SelectedOperationType)); OnPropertyChanged(nameof(EmployeeComboVisibility)); }
        }

        private User _selectedEmployee;
        public User SelectedEmployee { get => _selectedEmployee; set { _selectedEmployee = value; OnPropertyChanged(nameof(SelectedEmployee)); } }

        public Visibility EmployeeComboVisibility => SelectedOperationType == "Аванс мойщику" ? Visibility.Visible : Visibility.Collapsed;

        public List<KeyValuePair<string, string>> OperationTypes { get; } = new List<KeyValuePair<string, string>>()
        {
            new KeyValuePair<string, string>("Приход", "Приход"), new KeyValuePair<string, string>("Расход", "Расход"),
            new KeyValuePair<string, string>("Аванс мойщику", "Аванс мойщику"), new KeyValuePair<string, string>("Размен", "Размен"),
            new KeyValuePair<string, string>("Инкассация", "Инкассация")
        };

        private void Close_Click(object sender, RoutedEventArgs e) => this.Visibility = Visibility.Collapsed;

        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
