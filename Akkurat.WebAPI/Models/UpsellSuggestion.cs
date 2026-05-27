using System.ComponentModel.DataAnnotations;

namespace Akkurat.WebAPI.Models
{
    public class UpsellSuggestion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TriggerServiceId { get; set; } // При какой услуге предлагать

        [Required]
        public int SuggestedServiceId { get; set; } // Какую услугу предлагать (ID из таблицы Services)

        [Required]
        public string Message { get; set; } // Текст для админа: "Предложи антидождь!"

        public decimal BonusAmount { get; set; } // Бонус админу в рублях
    }
}
