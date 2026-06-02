using AccuratPanelCWM.ViewModels;
using Microsoft.Maui.Controls;

namespace AccuratPanelCWM.Views
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