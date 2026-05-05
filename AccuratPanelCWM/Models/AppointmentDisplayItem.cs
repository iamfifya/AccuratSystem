// Models/AppointmentDisplayItem.cs
using System;
using System.ComponentModel;

namespace AccuratPanelCWM.Models
{
    public class AppointmentDisplayItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private int _id;
        private string _carNumber;
        private string _carModel;
        private DateTime _time;
        private DateTime _endTime;
        private string _servicesList;
        private decimal _finalPrice;
        private decimal _extraCost;
        private string _extraCostReason;
        private int _boxNumber;
        private string _status;
        private bool _isCompleted;
        private bool _isSelected;

        public int Id
        {
            get => _id;
            set { _id = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Id))); }
        }

        public string CarNumber
        {
            get => _carNumber;
            set { _carNumber = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CarNumber))); }
        }

        public string CarModel
        {
            get => _carModel;
            set { _carModel = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CarModel))); }
        }

        public DateTime Time
        {
            get => _time;
            set { _time = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Time))); }
        }

        public DateTime EndTime
        {
            get => _endTime;
            set { _endTime = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EndTime))); }
        }

        public string ServicesList
        {
            get => _servicesList;
            set { _servicesList = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ServicesList))); }
        }

        public decimal FinalPrice
        {
            get => _finalPrice;
            set { _finalPrice = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FinalPrice))); }
        }

        public decimal ExtraCost
        {
            get => _extraCost;
            set { _extraCost = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExtraCost))); }
        }

        public string ExtraCostReason
        {
            get => _extraCostReason;
            set { _extraCostReason = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExtraCostReason))); }
        }

        public int BoxNumber
        {
            get => _boxNumber;
            set { _boxNumber = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BoxNumber))); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status))); }
        }

        public bool IsCompleted
        {
            get => _isCompleted;
            set { _isCompleted = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCompleted))); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }
}
