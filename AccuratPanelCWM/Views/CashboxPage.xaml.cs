using AccuratPanelCWM.ViewModels;
using Microsoft.Maui.Controls;

namespace AccuratPanelCWM.Views
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