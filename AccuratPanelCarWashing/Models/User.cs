using AccuratSystem.Contracts.Models;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AccuratPanelCarWashing.Models
{
    public class User : AccuratSystem.Contracts.Models.User, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        [JsonIgnore]
        public bool IsAdmin => Role == 1 || Role == 2;

        // 🔧 ДОБАВЛЕНО: Свойство для отображения в UI
        [JsonIgnore]
        public string RoleDisplay
        {
            get
            {
                switch (Role)
                {
                    case 1: return "👑 Директор";
                    case 2: return "🛡️ Администратор";
                    case 3: return "🧽 Мойщик";
                    case 4: return "🔧 Сервис";
                    default: return "👤 Сотрудник";
                }
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