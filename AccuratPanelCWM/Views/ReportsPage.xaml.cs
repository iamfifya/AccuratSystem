using AccuratPanelCWM.ViewModels;
using Microsoft.Maui.Controls;

namespace AccuratPanelCWM.Views
{
    public partial class ReportsPage : ContentPage
    {
        public ReportsPage(ReportsViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            var vm = (ReportsViewModel)BindingContext;

            // Запрашиваем данные только если список смен пуст (при первом открытии вкладки)
            if (vm.Reports.Count == 0)
            {
                vm.LoadReportCommand.Execute(null);
            }
        }
    }
}