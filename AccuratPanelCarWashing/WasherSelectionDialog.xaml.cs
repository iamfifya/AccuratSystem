using AccuratSystem.Contracts.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace AccuratPanelCarWashing
{
    public partial class WasherSelectionDialog : Window
    {
        public User SelectedWasher { get; private set; }
        private List<User> _washers; // Сохраняем список для поиска

        public WasherSelectionDialog(List<User> washers)
        {
            InitializeComponent();
            _washers = washers ?? new List<User>();
            WasherComboBox.ItemsSource = _washers;

            if (_washers.Any())
            {
                WasherComboBox.SelectedItem = _washers.FirstOrDefault();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            User selected = null;

            // Вариант 1: Пытаемся получить через SelectedItem (если сработал каст)
            if (WasherComboBox.SelectedItem is User selUser)
            {
                selected = selUser;
            }
            // Вариант 2: Через SelectedValue (Id), так как задан SelectedValuePath="Id"
            else if (WasherComboBox.SelectedValue is int selectedId)
            {
                selected = _washers.FirstOrDefault(u => u.Id == selectedId);
            }
            // Вариант 3: Фоллбэк через Text (если IsEditable=True и пользователь выбрал/ввел текст)
            else if (!string.IsNullOrWhiteSpace(WasherComboBox.Text))
            {
                string text = WasherComboBox.Text.Trim();
                selected = _washers.FirstOrDefault(u =>
                    u.FullName != null && u.FullName.IndexOf(text, System.StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (selected != null)
            {
                SelectedWasher = selected;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Выберите мойщика из списка!", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}