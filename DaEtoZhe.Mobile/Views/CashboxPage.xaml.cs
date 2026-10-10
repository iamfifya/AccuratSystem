using DaEtoZhe.Mobile.ViewModels;
using Microsoft.Maui.Controls;

namespace DaEtoZhe.Mobile.Views
{
    public partial class CashboxPage : ContentPage
    {
        public CashboxPage(CashboxViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}