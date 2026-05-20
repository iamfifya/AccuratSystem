using AccuratSystem.Contracts.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace AccuratPanelCarWashing
{
    public partial class WasherSelectionDialog : Window
    {
        public User SelectedWasher { get; private set; }

        public WasherSelectionDialog(List<User> washers)
        {
            InitializeComponent();
            WasherComboBox.ItemsSource = washers;

            if (washers.Any())
            {
                // Выбираем первого мойщика по умолчанию
                WasherComboBox.SelectedItem = washers.FirstOrDefault();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (WasherComboBox.SelectedItem is User selected)
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
