using DaEtoZhe.Mobile.ViewModels;
using Microsoft.Maui.Controls;

namespace DaEtoZhe.Mobile.Views
{
    public partial class SettingsPage : ContentPage
    {
        public SettingsPage(SettingsViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}