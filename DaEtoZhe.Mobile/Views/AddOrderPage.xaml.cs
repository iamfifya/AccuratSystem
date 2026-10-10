using DaEtoZhe.Mobile.ViewModels;
using Microsoft.Maui.Controls;

namespace DaEtoZhe.Mobile.Views
{
    public partial class AddOrderPage : ContentPage
    {
        public AddOrderPage(AddOrderViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}