using System;
using System.Collections.Generic;
using AccuratSystem.Contracts.DTOs;

namespace AccuratSystem.Contracts.Models
{
    public class BaseReport
    {
        public int TotalCars { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalWasherEarnings { get; set; }
        public decimal TotalCompanyEarnings { get; set; }

        public int WashTotalCars { get; set; }
        public decimal WashTotalRevenue { get; set; }
        public decimal WashCompanyEarnings { get; set; }
        public decimal WashTotalExpenses { get; set; }
        public decimal WashNetProfit { get { return WashCompanyEarnings - WashTotalExpenses; } }

        public int ServiceTotalCars { get; set; }
        public decimal ServiceTotalRevenue { get; set; }
        public decimal ServiceCompanyEarnings { get; set; }
        public decimal ServiceTotalExpenses { get; set; }
        public decimal ServiceNetProfit { get { return ServiceCompanyEarnings - ServiceTotalExpenses; } }

        public int CashCount { get; set; }
        public decimal CashAmount { get; set; }
        public int CardCount { get; set; }
        public decimal CardAmount { get; set; }
        public int TransferCount { get; set; }
        public decimal TransferAmount { get; set; }
        public int QrCount { get; set; }
        public decimal QrAmount { get; set; }

        public int UniqueClientsCount { get; set; }
        public int NewClientsCount { get; set; }
        public decimal TotalAdvances { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetProfit { get { return TotalCompanyEarnings - TotalExpenses; } }

        // === Средний чек ===
        public decimal AverageCheck { get; set; }

        // === Аналитика по скидкам ===
        public decimal TotalDiscountAmount { get; set; }
        public int DiscountedOrdersCount { get; set; }

        // === Топ услуг ===
        public List<ServiceAnalytics> TopServices { get; set; } = new List<ServiceAnalytics>();

        // === Разбивка расходов по категориям ===
        public List<ExpenseCategoryReport> ExpensesByCategory { get; set; } = new List<ExpenseCategoryReport>();

        // === Загруженность по часам ===
        public List<HourlyLoad> HourlyLoad { get; set; } = new List<HourlyLoad>();

        // === Остаток по кассе ===
        public decimal ExpectedCashBalance { get; set; }
        public decimal ActualCashBalance { get; set; }
        public decimal CashBalanceDifference { get; set; }

        /// <summary>Себестоимость материалов, съеденных заказами периода
        /// (автосписания минус сторно), ₽. Информационная метрика:
        /// в NetProfit НЕ входит (отчёты остаются кассовыми).</summary>
        public decimal TotalStockConsumption { get; set; }

        public decimal ConsumptionChange { get; set; }
        public decimal ConsumptionChangePercent { get; set; }

        public List<EmployeeReport> EmployeesWork { get; set; } = new List<EmployeeReport>();
    }

    // === НОВЫЕ КЛАССЫ ДЛЯ АНАЛИТИКИ ===

    /// <summary>
    /// Категория расходов
    /// </summary>
    public class ExpenseCategoryReport
    {
        public string Category { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }

    /// <summary>
    /// Загруженность по часам
    /// </summary>
    public class HourlyLoad
    {
        public int Hour { get; set; }
        public string HourLabel { get; set; } = string.Empty;
        public int OrdersCount { get; set; }
        public decimal Revenue { get; set; }
    }

    /// <summary>
    /// Результат сравнения двух периодов
    /// </summary>
    public class PeriodComparison
    {
        public BaseReport CurrentPeriod { get; set; }
        public BaseReport PreviousPeriod { get; set; }

        public decimal RevenueChange { get; set; }
        public decimal RevenueChangePercent { get; set; }
        public decimal ProfitChange { get; set; }
        public decimal ProfitChangePercent { get; set; }
        public int CarsChange { get; set; }
        public decimal CarsChangePercent { get; set; }
        public decimal AvgCheckChange { get; set; }
        public decimal AvgCheckChangePercent { get; set; }
    }

    public class ShiftReport : BaseReport
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class CustomPeriodReport : BaseReport
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<DailyReportSummary> DailyReports { get; set; } = new List<DailyReportSummary>();
        public string BranchName { get; set; } = string.Empty;

        public decimal TotalStockConsumption { get; set; }
        public List<StockConsumptionItem> StockConsumptionByItem { get; set; } = new List<StockConsumptionItem>();
        public List<StockLowItem> LowStock { get; set; } = new List<StockLowItem>();
    }

    public class DailyReportSummary : BaseReport
    {
        public DateTime Date { get; set; }
    }

    public class EmployeeReport
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public int CarsWashed { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Earnings { get; set; }
        public decimal Advances { get; set; }
        public decimal ToPay { get { return Math.Max(0, Earnings - Advances); } }
        public List<DailyEmployeeReport> DailyWork { get; set; } = new List<DailyEmployeeReport>();
    }

    public class DailyEmployeeReport
    {
        public DateTime Date { get; set; }
        public int CarsWashed { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Earnings { get; set; }
    }

    public class ServiceAnalytics
    {
        public string ServiceName { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}