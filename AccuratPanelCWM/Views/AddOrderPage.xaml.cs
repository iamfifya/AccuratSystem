using AccuratPanelCWM.ViewModels;
using Microsoft.Maui.Controls;

namespace AccuratPanelCWM.Views
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