// Файл: ViewModels/PriceEntryViewModel.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AccuratPanelCarWashing.ViewModels
{
    public class PriceEntryViewModel : INotifyPropertyChanged
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }

        private decimal _price;
        public decimal Price
        {
            get => _price;
            set { _price = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
