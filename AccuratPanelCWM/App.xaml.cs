using Microsoft.Maui.Controls;

namespace AccuratPanelCWM
{
    public partial class App : Application
    {
        // 💥 Просим готовую страницу прямо в конструкторе!
        public App(Views.LoginPage loginPage)
        {
            InitializeComponent();

            // Приложение всегда должно начинаться с экрана логина
            MainPage = loginPage;

            // Проверяем настройки темы при старте
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