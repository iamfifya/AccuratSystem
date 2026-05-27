using AccuratSystem.Contracts.Models;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AccuratPanelCarWashing.Models
{
    public class User : AccuratSystem.Contracts.Models.User, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Свойство для красивого отображения роли
        public string RoleDisplay
        {
            get
            {
                switch (this.Role)
                {
                    case 1: return "👑 Директор";
                    case 2: return "⚙️ Администратор";
                    case 3: return "🛠️ Сотрудник сервиса";
                    case 4: return "👤 Мойщик";
                    default: return "👤 Сотрудник";
                }
            }
        }


        [JsonIgnore]
        public bool IsAdmin
        {
            get { return Role == 1 || Role == 2; }
            set
            {
                Role = value ? 2 : 3;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAdmin));
                OnPropertyChanged(nameof(RoleDisplay)); // Обновляем и роль тоже
            }
        }

        [JsonIgnore]
        public string DisplayString
        {
            get
            {
                string phonePart = string.IsNullOrEmpty(Phone) ? "" : $"| {Phone} ";
                return $"{FullName} {RoleDisplay} {phonePart}";
            }
        }
    }
}
