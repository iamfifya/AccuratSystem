using System;
using System.Windows;

namespace AccuratPanelCWD.Services
{
    public enum AppTheme
    {
        Light,
        Dark
    }

    public static class ThemeManager
    {
        private static AppTheme _currentTheme = AppTheme.Light;
        public static AppTheme CurrentTheme => _currentTheme;

        public static event EventHandler ThemeChanged;

        public static void SetTheme(AppTheme theme)
        {
            if (_currentTheme == theme) return;

            _currentTheme = theme;
            ApplyTheme(theme);
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void ApplyTheme(AppTheme theme)
        {
            var dictionaries = Application.Current.Resources.MergedDictionaries;

            // Находим и удаляем старую тему
            ResourceDictionary themeDict = null;
            foreach (var dict in dictionaries)
            {
                if (dict.Source?.ToString().Contains("Theme") == true)
                {
                    themeDict = dict;
                    break;
                }
            }

            if (themeDict != null)
                dictionaries.Remove(themeDict);

            // Добавляем новую тему
            var themeUri = theme == AppTheme.Dark
                ? new Uri("Styles/DarkTheme.xaml", UriKind.Relative)
                : new Uri("Styles/LightTheme.xaml", UriKind.Relative);

            dictionaries.Add(new ResourceDictionary { Source = themeUri });
        }

        public static void ToggleTheme()
        {
            SetTheme(_currentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);
        }
    }
}