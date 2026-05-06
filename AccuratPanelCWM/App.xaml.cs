namespace AccuratPanelCWM
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Приложение всегда должно начинаться с экрана логина!
            MainPage = new Views.LoginPage();
        }
    }
}