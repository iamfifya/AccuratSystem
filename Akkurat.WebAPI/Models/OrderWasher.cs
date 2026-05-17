using System.Text.Json.Serialization; // 🔥 Обязательно добавь этот using

namespace Accurat.WebAPI.Models
{
    public class OrderWasher
    {
        public int OrderId { get; set; }

        [JsonIgnore] // 🔥 Говорим серверу не ждать этот объект от клиента
        public Order? Order { get; set; }

        public int UserId { get; set; }

        [JsonIgnore] // 🔥 И этот тоже
        public User? Washer { get; set; }

        public decimal SplitShare { get; set; }
    }
}