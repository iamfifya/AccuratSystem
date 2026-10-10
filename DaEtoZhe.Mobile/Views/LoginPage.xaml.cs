using DaEtoZhe.Mobile.ViewModels;
using Microsoft.Maui.Controls;

namespace DaEtoZhe.Mobile.Views
{
    public partial class LoginPage : ContentPage
    {
        // Внедряем ViewModel через конструктор
        public LoginPage(LoginViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}