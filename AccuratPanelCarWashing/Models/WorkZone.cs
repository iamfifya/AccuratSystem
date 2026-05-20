using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AccuratPanelCarWashing.Models
{
    /// <summary>
    /// UI-представление рабочей зоны (бокс мойки или подъёмник сервиса).
    /// </summary>
    public class WorkZone : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public int ZoneNumber { get; set; }
        public string ZoneName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty; // "Wash" или "Service"

        public ObservableCollection<OrderDisplayItem> Orders { get; set; } = new ObservableCollection<OrderDisplayItem>();
    }
}