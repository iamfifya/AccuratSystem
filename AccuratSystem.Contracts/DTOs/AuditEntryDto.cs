using System;

namespace AccuratSystem.Contracts.DTOs
{
    /// <summary>
    /// Унифицированная запись журнала действий.
    /// Может описывать событие заказа, смены или сотрудника.
    /// </summary>
    public class AuditEntryDto
    {
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Тип события (строка, т.к. источники разные:
        /// PriceChanged, DiscountApplied, ShiftOpened, ShiftClosed, 
        /// CashReconciliation, StatusChanged и т.д.)
        /// </summary>
        public string EventType { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>ID пользователя (для фильтра и клика по имени)</summary>
        public int? RelatedEntityId { get; set; }

        /// <summary>ID заказа (если событие привязано к заказу)</summary>
        public int? OrderId { get; set; }

        /// <summary>ID смены (если событие привязано к смене)</summary>
        public int? ShiftId { get; set; }

        /// <summary>ID филиала (для фильтрации)</summary>
        public int BranchId { get; set; }
    }
}