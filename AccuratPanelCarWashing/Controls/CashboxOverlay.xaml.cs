// === ЯВНЫЕ АЛИАСЫ ДЛЯ РАЗРЕШЕНИЯ КОНФЛИКТОВ ИМЁН ===
// UI-модели (с INotifyPropertyChanged) — используем в коллекции ActiveEmployees
using WpfUser = AccuratPanelCarWashing.Models.User;
// Контрактные модели из API — используем для данных с сервера
using ContractsUser = AccuratSystem.Contracts.Models.User;
using ContractsTransaction = AccuratSystem.Contracts.Models.Transaction;
using ContractsShift = AccuratSystem.Contracts.Models.Shift;

// Остальные using
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
using AccuratPanelCarWashing.Models;

namespace AccuratPanelCarWashing.Controls
{
    public partial class CashboxOverlay : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly ApiService _apiService = new ApiService();
        private ContractsShift _currentShift;

        public ObservableCollection<ContractsTransaction> Transactions { get; set; } = new ObservableCollection<ContractsTransaction>();
        public ObservableCollection<WpfUser> ActiveEmployees { get; set; } = new ObservableCollection<WpfUser>();

        private decimal _cashInHand, _totalExpenses, _netCashProfit;
        public decimal CashInHand { get => _cashInHand; set { _cashInHand = value; OnPropertyChanged(nameof(CashInHand)); } }
        public decimal TotalExpenses { get => _totalExpenses; set { _totalExpenses = value; OnPropertyChanged(nameof(TotalExpenses)); } }
        public decimal NetCashProfit { get => _netCashProfit; set { _netCashProfit = value; OnPropertyChanged(nameof(NetCashProfit)); } }

        public CashboxOverlay()
        {
            InitializeComponent();
            DataContext = this;
        }

        public void Show(ContractsShift shift)
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
            var allUsers = await _apiService.GetUsersAsync(); // Возвращает List<ContractsUser>
            foreach (var empId in _currentShift.EmployeeIds)
            {
                var user = allUsers.FirstOrDefault(u => u.Id == empId);
                if (user != null)
                {
                    // Создаём UI-обёртку на основе контрактного пользователя
                    ActiveEmployees.Add(new WpfUser
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        Phone = user.Phone,
                        Login = user.Login,
                        PasswordHash = user.PasswordHash,
                        Role = user.Role,
                        IsActive = user.IsActive,
                        BranchId = user.BranchId,
                        BaseWagePercentage = user.BaseWagePercentage
                    });
                }
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
            var newTransaction = new ContractsTransaction
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

        private WpfUser _selectedEmployee;
        public WpfUser SelectedEmployee { get => _selectedEmployee; set { _selectedEmployee = value; OnPropertyChanged(nameof(SelectedEmployee)); } }

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