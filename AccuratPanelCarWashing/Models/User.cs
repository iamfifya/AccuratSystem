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
        public bool IsAdmin => RoleId == 1 || RoleId == 2;

        // 💥 ИСПРАВЛЕНИЕ: Никаких switch! Просто берем название из базы
        [JsonIgnore]
        public string RoleDisplay => Role != null ? Role.Name : "👤 Сотрудник";

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