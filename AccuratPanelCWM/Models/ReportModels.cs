using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AccuratPanelCWM.Models
{
    // --- 1. Базовый класс для отчетов ---
    public class BaseReport
    {
        public int TotalCars { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalWasherEarnings { get; set; }
        public decimal TotalCompanyEarnings { get; set; }

        // МОЙКА
        public int WashTotalCars { get; set; }
        public decimal WashTotalRevenue { get; set; }
        public decimal WashCompanyEarnings { get; set; }
        public decimal WashTotalExpenses { get; set; }
        public decimal WashNetProfit => WashCompanyEarnings - WashTotalExpenses;

        // СЕРВИС
        public int ServiceTotalCars { get; set; }
        public decimal ServiceTotalRevenue { get; set; }
        public decimal ServiceCompanyEarnings { get; set; }
        public decimal ServiceTotalExpenses { get; set; }
        public decimal ServiceNetProfit => ServiceCompanyEarnings - ServiceTotalExpenses;

        [JsonProperty("TotalCashCount")]
        public int CashCount { get; set; }
        [JsonProperty("TotalCashAmount")]
        public decimal CashAmount { get; set; }

        [JsonProperty("TotalCardCount")]
        public int CardCount { get; set; }
        [JsonProperty("TotalCardAmount")]
        public decimal CardAmount { get; set; }

        [JsonProperty("TotalTransferCount")]
        public int TransferCount { get; set; }
        [JsonProperty("TotalTransferAmount")]
        public decimal TransferAmount { get; set; }

        [JsonProperty("TotalQrCount")]
        public int QrCount { get; set; }
        [JsonProperty("TotalQrAmount")]
        public decimal QrAmount { get; set; }

        // Поля аналитики клиентов
        public int UniqueClientsCount { get; set; }
        public int NewClientsCount { get; set; }

        // Финансы и касса
        public decimal TotalAdvances { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetProfit
        {
            get { return TotalCompanyEarnings - TotalExpenses; }
        }

        public List<EmployeeReport> EmployeesWork { get; set; } = new List<EmployeeReport>();
    }

    // --- 2. Модель для конкретной смены (Ежедневный отчет) ---
    public class ShiftReport : BaseReport
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Notes { get; set; }
    }

    // --- 3. Модель для Выборочного (Интервального) отчета ---
    public class CustomPeriodReport : BaseReport
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<DailyReportSummary> DailyReports { get; set; } = new List<DailyReportSummary>();
        public string BranchName { get; set; }
    }

    // --- 4. Вспомогательные классы ---
    public class DailyReportSummary : BaseReport
    {
        public DateTime Date { get; set; }
    }

    public class EmployeeReport
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public int CarsWashed { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Earnings { get; set; } // Итого начислено (35% + минималка)

        public decimal Advances { get; set; } // Взято авансов
        public decimal ToPay
        {
            get { return Math.Max(0, Earnings - Advances); } // Итого к выдаче
        }

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
        public string ServiceName { get; set; }
        public int Count { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}
