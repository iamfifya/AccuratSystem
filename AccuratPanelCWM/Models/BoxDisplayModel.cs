using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AccuratPanelCWM.Models
{
    public class BoxDisplayModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Вместо BoxId теперь используем номер и департамент
        public int BoxNumber { get; set; }
        public string Department { get; set; } // "Wash" или "Service"

        public string BoxName { get; set; }

        private CarWashOrder _currentOrder;
        public CarWashOrder CurrentOrder
        {
            get => _currentOrder;
            set
            {
                _currentOrder = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsFree));
            }
        }

        public bool IsBusy => CurrentOrder != null;
        public bool IsFree => CurrentOrder == null;
    }
}