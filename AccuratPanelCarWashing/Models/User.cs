using Newtonsoft.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices; // Добавь этот using для [CallerMemberName]

namespace AccuratPanelCarWashing.Models
{
    public class User : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // Метод для уведомления интерфейса об изменениях
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public int Id { get; set; }

        private string _fullName;
        public string FullName
        {
            get => _fullName;
            set { _fullName = value; OnPropertyChanged(); }
        }

        public string Login { get; set; }
        public string PasswordHash { get; set; }

        private int _role;
        public int Role
        {
            get => _role;
            set
            {
                if (_role != value)
                {
                    _role = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsAdmin)); // Уведомляем, что IsAdmin тоже мог измениться
                }
            }
        }

        // 🔥 Базовый процент зарплаты сотрудника (по умолчанию 35%)
        public decimal BaseWagePercentage { get; set; } = 35m;

        [JsonIgnore]
        public bool IsAdmin
        {
            get => Role == 1 || Role == 2;
            set
            {
                Role = value ? 2 : 3;
                OnPropertyChanged();
            }
        }

        public bool IsActive { get; set; } = true;

        private string _phone;
        public string Phone
        {
            get => _phone;
            set
            {
                if (_phone != value)
                {
                    _phone = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public string DisplayString => $"{FullName} {(IsAdmin ? "👑" : "👤")} {(string.IsNullOrEmpty(Phone) ? "" : $"| {Phone}")}";
    }
}
