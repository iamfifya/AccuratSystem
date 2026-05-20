using System;
using System.Collections.Generic;

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

        public List<EmployeeReport> EmployeesWork { get; set; } = new List<EmployeeReport>();
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