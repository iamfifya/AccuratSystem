using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AccuratPanelCarWashing.Models
{
    public class AppointmentDisplayItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private int _id;
        public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }

        private string _carNumber;
        public string CarNumber { get => _carNumber; set { _carNumber = value; OnPropertyChanged(); } }

        private string _carModel;
        public string CarModel { get => _carModel; set { _carModel = value; OnPropertyChanged(); } }

        private DateTime _time;
        public DateTime Time { get => _time; set { _time = value; OnPropertyChanged(); } }

        private DateTime _endTime;
        public DateTime EndTime { get => _endTime; set { _endTime = value; OnPropertyChanged(); } }

        private string _servicesList;
        public string ServicesList { get => _servicesList; set { _servicesList = value; OnPropertyChanged(); } }

        private decimal _finalPrice;
        public decimal FinalPrice { get => _finalPrice; set { _finalPrice = value; OnPropertyChanged(); } }

        private decimal _extraCost;
        public decimal ExtraCost { get => _extraCost; set { _extraCost = value; OnPropertyChanged(); } }

        private string _extraCostReason;
        public string ExtraCostReason { get => _extraCostReason; set { _extraCostReason = value; OnPropertyChanged(); } }

        private int _boxNumber;
        public int BoxNumber { get => _boxNumber; set { _boxNumber = value; OnPropertyChanged(); } }

        private string _status;
        public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }

        private bool _isCompleted;
        public bool IsCompleted { get => _isCompleted; set { _isCompleted = value; OnPropertyChanged(); } }

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}