using System;

namespace AccuratSystem.Contracts.Models
{
    /// <summary>
    /// Сверка кассы (X-отчет): снапшот пересчёта наличных.
    /// Expected считает сервер, Actual вносит сотрудник.
    /// </summary>
    public class CashReconciliation
    {
        public int Id { get; set; }
        public int ShiftId { get; set; }
        public Shift Shift { get; set; }

        /// <summary>
        /// Сколько наличных должно быть по данным системы на момент сверки
        /// </summary>
        public decimal ExpectedCash { get; set; }

        /// <summary>
        /// Сколько фактически насчитал сотрудник
        /// </summary>
        public decimal ActualCash { get; set; }

        /// <summary>
        /// Излишек (+) или недостача (−)
        /// </summary>
        public decimal Difference { get; set; }

        /// <summary>
        /// Кто пересчитывал (ID дублируем, чтобы отчёт не зависел от удаления юзера)
        /// </summary>
        public int? CountedById { get; set; }

        /// <summary>
        /// Имя пересчитывающего (строкой, для истории)
        /// </summary>
        public string CountedBy { get; set; } = string.Empty;

        public DateTime CountedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Комментарий (например, объяснение недостачи)
        /// </summary>
        public string Comment { get; set; } = string.Empty;
    }
}