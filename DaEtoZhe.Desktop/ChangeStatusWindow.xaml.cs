using DaEtoZhe.Contracts.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace DaEtoZhe.Desktop
{
    public partial class ChangeStatusWindow : Window
    {
        public VehicleStatus NewStatus { get; private set; }
        public string Notes { get; private set; } = string.Empty;

        private class StatusOption
        {
            public VehicleStatus Value { get; set; }
            public string Name { get; set; }
        }

        public ChangeStatusWindow(VehicleStatus currentStatus)
        {
            InitializeComponent();

            StatusCombo.ItemsSource = new List<StatusOption>
            {
                new StatusOption { Value = VehicleStatus.New,        Name = "🆕 Новый" },
                new StatusOption { Value = VehicleStatus.Purchase,   Name = " Куплен" },
                new StatusOption { Value = VehicleStatus.Appraisal,  Name = "🔍 Оценен" },
                new StatusOption { Value = VehicleStatus.Repair,     Name = "🔧 В ремонте" },
                new StatusOption { Value = VehicleStatus.Prep,       Name = "✨ Предпродажная подготовка" },
                new StatusOption { Value = VehicleStatus.Listed,     Name = "📢 Выставлен на продажу" },
                new StatusOption { Value = VehicleStatus.Reserved,   Name = "🔒 Зарезервирован" },
                new StatusOption { Value = VehicleStatus.Sold,       Name = "💰 Продан" },
                new StatusOption { Value = VehicleStatus.WrittenOff, Name = "♻️ Списан" },
            };

            StatusCombo.SelectedValue = StatusCombo.ItemsSource
                .Cast<StatusOption>()
                .Any(o => o.Value == currentStatus)
                ? currentStatus
                : VehicleStatus.Purchase;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (StatusCombo.SelectedValue is not VehicleStatus status)
            {
                MessageBox.Show("Выберите статус!", "Внимание");
                return;
            }
            NewStatus = status;
            Notes = NotesTextBox.Text?.Trim() ?? "";
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}