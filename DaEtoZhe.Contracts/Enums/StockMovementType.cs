namespace DaEtoZhe.Contracts.Enums
{
    /// <summary>Тип движения складских остатков.</summary>
    public enum StockMovementType
    {
        Receipt = 0,     // приход от поставщика
        WriteOff = 1,    // списание (ручное; с этапа 3 — также по нормам)
        TransferIn = 2,  // перемещение: вход в филиал (этап 4)
        TransferOut = 3, // перемещение: выход из филиала (этап 4)
        Adjustment = 4,  // корректировка по инвентаризации (этап 4)
        Storno = 5       // сторно автосписания (этап 3)
    }
}