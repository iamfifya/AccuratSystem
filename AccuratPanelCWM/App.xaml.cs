using AccuratPanelCWM.Views;

namespace AccuratPanelCWM
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // При запуске показываем окно авторизации, а не Shell
            MainPage = new LoginPage();
        }
    }
}