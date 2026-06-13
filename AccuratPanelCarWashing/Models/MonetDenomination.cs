using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AccuratPanelCarWashing.Models
{
    public class MoneyDenomination : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private int _count;
        public int Count
        {
            get => _count;
            set { _count = value; OnPropertyChanged(); }
        }

        public int Value { get; set; } // Номинал (5000, 2000 и т.д.)
        public string DisplayName => $"{Value} ₽";

        public decimal Total => Count * Value;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
