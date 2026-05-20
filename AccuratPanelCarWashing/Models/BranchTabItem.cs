using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AccuratPanelCarWashing.Models
{
    public class BranchTabItem : INotifyPropertyChanged
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public string Address { get; set; }

        public ObservableCollection<WorkZone> WashZones { get; set; } = new ObservableCollection<WorkZone>();
        public ObservableCollection<WorkZone> ServiceZones { get; set; } = new ObservableCollection<WorkZone>();

        public bool HasWashZones => WashZones.Count > 0;
        public bool HasServiceZones => ServiceZones.Count > 0;

        public ObservableCollection<WorkZone> BranchWorkZones { get; set; } = new ObservableCollection<WorkZone>();
        public decimal TotalRevenue { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}