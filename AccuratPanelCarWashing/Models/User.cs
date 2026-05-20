using AccuratSystem.Contracts.Models;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AccuratPanelCarWashing.Models
{
    /// <summary>
    /// UI-обёртка над контрактным User.
    /// Добавляет свойства для интерфейса: IsAdmin, DisplayString, PropertyChanged.
    /// </summary>
    public class User : AccuratSystem.Contracts.Models.User, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
            }
        }

        [JsonIgnore]
        public string DisplayString
        {
            get
            {
                string roleIcon = IsAdmin ? "👑 " : "👤 ";
                string phonePart = string.IsNullOrEmpty(Phone) ? "" : $"| {Phone} ";
                return $"{FullName} {roleIcon}{phonePart}";
            }
        }
    }
}