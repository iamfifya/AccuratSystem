// ServiceViewModel.cs
using System;
using System.ComponentModel;

namespace AccuratPanelCarWashing.ViewModels
{
    public class ServiceViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public int Id { get; set; }
        public string Name { get; set; }
        private decimal _price;
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));

                    // ← НОВОЕ: сообщаем об изменении выбора
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        // ← НОВОЕ: событие для подписки
        public event EventHandler SelectionChanged;

        public decimal Price
        {
            get => _price;
            set
            {
                if (_price != value)
                {
                    System.Diagnostics.Debug.WriteLine($"[ServiceVM] Price changed: {Name} {_price:N0} → {value:N0} ₽");
                    _price = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Price)));
                }
            }
        }
    }
}
