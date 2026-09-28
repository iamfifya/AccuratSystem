namespace AccuratSystem.Contracts.Enums
{
    /// <summary>Тип складского документа.</summary>
    public enum StockDocumentType
    {
        Receipt = 0,   // приход
        WriteOff = 1,  // списание
        Transfer = 2,  // перемещение (этап 4)
        Stocktake = 3  // инвентаризация (этап 4)
    }
}