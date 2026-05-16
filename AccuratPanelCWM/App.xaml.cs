namespace AccuratPanelCWM
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Приложение всегда должно начинаться с экрана логина!
            MainPage = new Views.LoginPage();

            // 🔥 Проверяем настройки темы при старте
            string savedTheme = Microsoft.Maui.Storage.Preferences.Default.Get("AppTheme", "System");

            switch (savedTheme)
            {
                case "System":
                    Application.Current.UserAppTheme = AppTheme.Unspecified;
                    break;
                case "Light":
                    Application.Current.UserAppTheme = AppTheme.Light;
                    break;
                case "Dark":
                    Application.Current.UserAppTheme = AppTheme.Dark;
                    break;
            }
        }
    }
}