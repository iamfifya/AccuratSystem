using System;

namespace AccuratSystem.Contracts.Models
{
    /// <summary>
    /// Модель истории статусов. 
    /// Позволяет точно знать, сколько времени заказ провел в каждом состоянии.
    /// </summary>
    public class OrderStatusHistory
    {
        public int Id { get; set; }
        public int OrderId { get; set; }

        // Статус, в котором находился заказ (напр. "В работе", "Диагностика", "Ожидание запчастей")
        public string Status { get; set; } = string.Empty;

        // Когда заказ перешел в этот статус
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        // Когда заказ покинул этот статус. 
        // Если здесь NULL — значит заказ всё еще находится в этом статусе.
        public DateTime? EndTime { get; set; }

        // Кто перевел заказ в этот статус (ID пользователя)
        public int? UserId { get; set; }
    }
}
