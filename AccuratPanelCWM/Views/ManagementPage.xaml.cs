using AccuratPanelCWM.ViewModels;
using Microsoft.Maui.Controls;

namespace AccuratPanelCWM.Views
{
    public partial class ManagementPage : ContentPage
    {
        public ManagementPage(ManagementViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}