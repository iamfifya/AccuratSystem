using AccuratPanelCarWashing.Services;
using AccuratSystem.Contracts.Models;
using System;
using System.ComponentModel;

namespace AccuratPanelCarWashing.ViewModels
{
    public class TimelineEntryViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string CreatedBy { get; set; }
        public string EntryType { get; set; }
        public string Message { get; set; }
        public int? RelatedEntityId { get; set; }

        // Иконка для типа записи
        public string EntryTypeIcon
        {
            get
            {
                switch (EntryType)
                {
                    case "Comment": return "💬";
                    case "StatusChanged": return "🔄";
                    case "PriceChanged": return "💰";
                    case "ExpenseAdded": return "🛒";
                    default: return "📝";
                }
            }
        }

        // Доп. информация о связанной сущности
        public string RelatedEntityInfo
        {
            get
            {
                if (!RelatedEntityId.HasValue) return string.Empty;
                return $"ID: {RelatedEntityId.Value}";
            }
        }

        public TimelineEntryViewModel(OrderTimelineEntry entry)
        {
            Id = entry.Id;
            Timestamp = TimeHelper.ToMsk(entry.Timestamp); // Конвертация в московское время
            CreatedBy = entry.CreatedBy ?? string.Empty;
            EntryType = entry.EntryType.ToString();
            Message = entry.Message;
            RelatedEntityId = entry.RelatedEntityId;
        }

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}