using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AccuratPanelCWM.Models
{
    public class BranchTabItem : INotifyPropertyChanged
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public string Address { get; set; }

        // Зоны именно этого филиала
        // public ObservableCollection<WorkZone> BranchWorkZones { get; set; } = new ObservableCollection<WorkZone>();

        // Можно добавить статистику конкретно для этой вкладки
        public decimal TotalRevenue { get; set; }
        // и т.д.

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}