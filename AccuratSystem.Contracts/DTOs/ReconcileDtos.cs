namespace AccuratSystem.Contracts.DTOs
{
    /// <summary>
    /// Запрос на сохранение пересчёта кассы
    /// </summary>
    public class ReconcileCashRequest
    {
        public decimal ActualCash { get; set; }
        public string Comment { get; set; } = "";
        public int? CountedById { get; set; }
        public string CountedBy { get; set; } = "";
    }

    /// <summary>
    /// Ответ сервера с результатом сверки
    /// </summary>
    public class ReconcileCashResult
    {
        public int Id { get; set; }
        public decimal ExpectedCash { get; set; }
        public decimal ActualCash { get; set; }
        public decimal Difference { get; set; }
    }
}