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

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // Каждый раз при открытии страницы обновляем статус смены
            if (BindingContext is ManagementViewModel vm)
            {
                vm.LoadShiftDataCommand.Execute(null);
            }
        }
    }
}
