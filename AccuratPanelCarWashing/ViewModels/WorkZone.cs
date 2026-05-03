// Models/WorkZone.cs
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AccuratPanelCarWashing.Models
{
    public class WorkZone : INotifyPropertyChanged
    {
        private string _zoneName;
        public string ZoneName
        {
            get => _zoneName;
            set { _zoneName = value; OnPropertyChanged(); }
        }

        private int _zoneNumber;
        public int ZoneNumber
        {
            get => _zoneNumber;
            set { _zoneNumber = value; OnPropertyChanged(); }
        }

        private string _department;
        public string Department
        {
            get => _department;
            set { _department = value; OnPropertyChanged(); }
        }

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        private ObservableCollection<OrderDisplayItem> _orders;
        public ObservableCollection<OrderDisplayItem> Orders
        {
            get => _orders;
            set { _orders = value; OnPropertyChanged(); }
        }

        public WorkZone()
        {
            Orders = new ObservableCollection<OrderDisplayItem>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}