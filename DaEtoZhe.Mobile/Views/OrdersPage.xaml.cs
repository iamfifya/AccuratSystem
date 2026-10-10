using DaEtoZhe.Mobile.ViewModels;
using Microsoft.Maui.Controls;

namespace DaEtoZhe.Mobile.Views
{
    public partial class OrdersPage : ContentPage
    {
        public OrdersPage(OrdersViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}