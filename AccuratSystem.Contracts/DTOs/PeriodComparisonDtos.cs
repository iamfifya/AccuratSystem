using System;
using System.Collections.Generic;
using AccuratSystem.Contracts.Models;

namespace AccuratSystem.Contracts.DTOs
{
    // ═══════════════════════════════════════════════════════
    //  КОРНЕВОЙ ОБЪЕКТ ОТВЕТА
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Полный результат сравнения двух периодов
    /// </summary>
    public class PeriodComparisonFull
    {
        // === Агрегированные данные периодов ===
        public BaseReport CurrentPeriod { get; set; } = new BaseReport();
        public BaseReport PreviousPeriod { get; set; } = new BaseReport();

        // === KPI-дельты ===
        public KpiDeltas Deltas { get; set; } = new KpiDeltas();

        // === Графики по дням ===
        public DailyComparisonData DailyData { get; set; } = new DailyComparisonData();

        // === Загруженность по часам ===
        public List<HourlyLoad> CurrentHourlyLoad { get; set; } = new List<HourlyLoad>();
        public List<HourlyLoad> PreviousHourlyLoad { get; set; } = new List<HourlyLoad>();

        // === Топ услуг (сравнение) ===
        public List<ServiceComparisonItem> ServicesComparison { get; set; } = new List<ServiceComparisonItem>();

        // === Расходы по категориям (сравнение) ===
        public List<ExpenseComparisonItem> ExpensesComparison { get; set; } = new List<ExpenseComparisonItem>();

        // === Клиенты ===
        public ClientComparisonData ClientsComparison { get; set; } = new ClientComparisonData();

        // === Департаменты ===
        public DepartmentComparisonData DepartmentsComparison { get; set; } = new DepartmentComparisonData();

        // === ФОТ сотрудников ===
        public FotComparisonData FotComparison { get; set; } = new FotComparisonData();

        // === Предупреждения ===
        public string Warning { get; set; } = string.Empty;
        public bool HasCurrentData { get; set; }
        public bool HasPreviousData { get; set; }
    }

    // ═══════════════════════════════════════════════════════
    //  KPI-ДЕЛЬТЫ
    // ═══════════════════════════════════════════════════════

    public class KpiDeltas
    {
        public decimal RevenueChange { get; set; }
        public decimal RevenueChangePercent { get; set; }

        public decimal ProfitChange { get; set; }
        public decimal ProfitChangePercent { get; set; }

        public int CarsChange { get; set; }
        public decimal CarsChangePercent { get; set; }

        public decimal AvgCheckChange { get; set; }
        public decimal AvgCheckChangePercent { get; set; }

        public decimal ExpensesChange { get; set; }
        public decimal ExpensesChangePercent { get; set; }

        public int NewClientsChange { get; set; }
        public decimal NewClientsChangePercent { get; set; }
    }

    // ═══════════════════════════════════════════════════════
    //  ГРАФИКИ ПО ДНЯМ
    // ═══════════════════════════════════════════════════════

    public class DailyComparisonData
    {
        public List<DayComparisonPoint> CurrentDays { get; set; } = new List<DayComparisonPoint>();
        public List<DayComparisonPoint> PreviousDays { get; set; } = new List<DayComparisonPoint>();
    }

    public class DayComparisonPoint
    {
        public DateTime Date { get; set; }
        public int DayIndex { get; set; }
        public string DateLabel { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int CarsCount { get; set; }
        public decimal AvgCheck { get; set; }
    }

    // ═══════════════════════════════════════════════════════
    //  СРАВНЕНИЕ УСЛУГ
    // ═══════════════════════════════════════════════════════

    public class ServiceComparisonItem
    {
        public string ServiceName { get; set; } = string.Empty;

        public int CurrentCount { get; set; }
        public decimal CurrentRevenue { get; set; }

        public int PreviousCount { get; set; }
        public decimal PreviousRevenue { get; set; }

        public decimal CountChangePercent { get; set; }
        public decimal RevenueChangePercent { get; set; }
    }

    // ═══════════════════════════════════════════════════════
    //  СРАВНЕНИЕ РАСХОДОВ
    // ═══════════════════════════════════════════════════════

    public class ExpenseComparisonItem
    {
        public string Category { get; set; } = string.Empty;

        public decimal CurrentAmount { get; set; }
        public int CurrentCount { get; set; }

        public decimal PreviousAmount { get; set; }
        public int PreviousCount { get; set; }

        public decimal AmountChangePercent { get; set; }
    }

    // ═══════════════════════════════════════════════════════
    //  СРАВНЕНИЕ КЛИЕНТОВ
    // ═══════════════════════════════════════════════════════

    public class ClientComparisonData
    {
        public int CurrentUnique { get; set; }
        public int PreviousUnique { get; set; }

        public int CurrentNew { get; set; }
        public int PreviousNew { get; set; }

        public int CurrentRepeat { get; set; }
        public int PreviousRepeat { get; set; }

        public decimal CurrentRetentionRate { get; set; }
        public decimal PreviousRetentionRate { get; set; }
    }

    // ═══════════════════════════════════════════════════════
    //  СРАВНЕНИЕ ДЕПАРТАМЕНТОВ
    // ═══════════════════════════════════════════════════════

    public class DepartmentComparisonData
    {
        public DepartmentComparison Wash { get; set; } = new DepartmentComparison();
        public DepartmentComparison Service { get; set; } = new DepartmentComparison();
    }

    public class DepartmentComparison
    {
        public decimal CurrentRevenue { get; set; }
        public decimal PreviousRevenue { get; set; }
        public decimal RevenueChangePercent { get; set; }

        public int CurrentCars { get; set; }
        public int PreviousCars { get; set; }
        public decimal CarsChangePercent { get; set; }

        public decimal CurrentProfit { get; set; }
        public decimal PreviousProfit { get; set; }
        public decimal ProfitChangePercent { get; set; }
    }

    // ═══════════════════════════════════════════════════════
    //  СРАВНЕНИЕ ФОТ
    // ═══════════════════════════════════════════════════════

    public class FotComparisonData
    {
        public decimal CurrentTotalFot { get; set; }
        public decimal PreviousTotalFot { get; set; }
        public decimal TotalFotChangePercent { get; set; }

        public decimal CurrentAvgPerEmployee { get; set; }
        public decimal PreviousAvgPerEmployee { get; set; }

        public int CurrentEmployeesCount { get; set; }
        public int PreviousEmployeesCount { get; set; }

        public List<EmployeeComparisonItem> Employees { get; set; } = new List<EmployeeComparisonItem>();
    }

    public class EmployeeComparisonItem
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;

        public decimal CurrentEarnings { get; set; }
        public decimal PreviousEarnings { get; set; }
        public decimal EarningsChangePercent { get; set; }

        public int CurrentCars { get; set; }
        public int PreviousCars { get; set; }

        public decimal CurrentAdvances { get; set; }
        public decimal PreviousAdvances { get; set; }
    }
}