using AccuratPanelCWM.ViewModels;
using Microsoft.Maui.Controls;

namespace AccuratPanelCWM.Views
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