using AccuratSystem.Contracts.DTOs;
using AccuratSystem.Contracts.Models;
using ClosedXML.Excel;
using System;
using System.Linq;
using System.Collections.Generic;

namespace AccuratPanelCWD
{
    public static class ExcelExporter
    {
        // Цвета
        private static readonly XLColor HeaderBg = XLColor.LightGray;
        private static readonly XLColor PositiveColor = XLColor.Green;
        private static readonly XLColor NegativeColor = XLColor.DarkRed;
        private static readonly XLColor NeutralColor = XLColor.Blue;

        // ═══════════════════════════════════════════════════════════
        // 1. ЭКСПОРТ ОДНОЙ СМЕНЫ (Расширенный)
        // ═══════════════════════════════════════════════════════════

        public static void ExportShiftReport(ShiftReport report, string filePath)
        {
            try
            {
                using (var workbook = new XLWorkbook())
                {
                    // === Лист 1: Сводка ===
                    ExportShiftSummary(workbook, report);

                    // === Лист 2: Способы оплаты ===
                    ExportPaymentMethods(workbook, report);

                    // === Лист 3: Сотрудники ===
                    ExportEmployees(workbook, report);

                    // === Лист 4: Топ услуг ===
                    ExportTopServices(workbook, report);

                    // === Лист 5: Расходы по категориям ===
                    ExportExpensesByCategory(workbook, report);

                    // === Лист 6: Загруженность по часам ===
                    ExportHourlyLoad(workbook, report);

                    // === Лист 7: Остаток кассы ===
                    ExportCashBalance(workbook, report);

                    workbook.SaveAs(filePath);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка экспорта в Excel: {ex.Message}");
            }
        }

        private static void ExportShiftSummary(XLWorkbook workbook, ShiftReport report)
        {
            var ws = workbook.Worksheets.Add("Сводка");

            // Заголовок
            ws.Cell(1, 1).Value = $"Отчет о смене от {report.Date:dd.MM.yyyy}";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Range(1, 1, 1, 6).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            int row = 3;

            // Основная информация
            ws.Cell(row, 1).Value = "Дата:";
            ws.Cell(row, 2).Value = report.Date.ToString("dd.MM.yyyy");
            row++;

            ws.Cell(row, 1).Value = "Время начала:";
            ws.Cell(row, 2).Value = report.StartTime.ToString("HH:mm");
            row++;

            ws.Cell(row, 1).Value = "Время окончания:";
            ws.Cell(row, 2).Value = report.EndTime?.ToString("HH:mm") ?? "Не закрыта";
            row += 2;

            // Финансовые показатели
            WriteSectionHeader(ws, ref row, "Финансовые показатели");
            WriteKpiRow(ws, ref row, "Всего машин", report.TotalCars, null);
            WriteKpiRow(ws, ref row, "Общая выручка", report.TotalRevenue, "#,##0.00 ₽");
            WriteKpiRow(ws, ref row, "Средний чек", report.AverageCheck, "#,##0.00 ₽");
            WriteKpiRow(ws, ref row, "Выплаты сотрудникам", report.TotalWasherEarnings, "#,##0.00 ₽");
            WriteKpiRow(ws, ref row, "Доход компании (до расходов)", report.TotalCompanyEarnings, "#,##0.00 ₽");
            WriteKpiRow(ws, ref row, "Расходы", report.TotalExpenses, "-#,##0.00 ₽", true);
            WriteKpiRow(ws, ref row, "Чистая прибыль (ЧПКО)", report.NetProfit, "#,##0.00 ₽", false, true);
            row++;

            // Аналитика по скидкам
            WriteSectionHeader(ws, ref row, "Аналитика по скидкам");
            WriteKpiRow(ws, ref row, "Сумма скидок", report.TotalDiscountAmount, "#,##0.00 ₽");
            WriteKpiRow(ws, ref row, "Заказов со скидкой", report.DiscountedOrdersCount, null);
            row++;

            // Департаменты
            WriteSectionHeader(ws, ref row, "МОЙКА");
            WriteKpiRow(ws, ref row, "Заказов", report.WashTotalCars, null);
            WriteKpiRow(ws, ref row, "Выручка", report.WashTotalRevenue, "#,##0.00 ₽");
            WriteKpiRow(ws, ref row, "Расходы", report.WashTotalExpenses, "-#,##0.00 ₽", true);
            WriteKpiRow(ws, ref row, "Прибыль", report.WashNetProfit, "#,##0.00 ₽", false, true);
            row++;

            WriteSectionHeader(ws, ref row, "АВТОСЕРВИС");
            WriteKpiRow(ws, ref row, "Заказов", report.ServiceTotalCars, null);
            WriteKpiRow(ws, ref row, "Выручка", report.ServiceTotalRevenue, "#,##0.00 ₽");
            WriteKpiRow(ws, ref row, "Расходы", report.ServiceTotalExpenses, "-#,##0.00 ₽", true);
            WriteKpiRow(ws, ref row, "Прибыль", report.ServiceNetProfit, "#,##0.00 ₽", false, true);

            ws.Columns().AdjustToContents();
        }

        private static void ExportPaymentMethods(XLWorkbook workbook, ShiftReport report)
        {
            var ws = workbook.Worksheets.Add("Способы оплаты");

            ws.Cell(1, 1).Value = "Статистика по способам оплаты";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            int row = 3;
            WriteTableHeader(ws, row, new[] { "Способ оплаты", "Количество", "Сумма", "% от выручки" });
            row++;

            var methods = new[]
            {
                ("💵 Наличные", report.CashCount, report.CashAmount),
                ("💳 Карта", report.CardCount, report.CardAmount),
                ("📱 Перевод", report.TransferCount, report.TransferAmount),
                ("🔲 QR-код", report.QrCount, report.QrAmount)
            };

            foreach (var (name, count, amount) in methods)
            {
                ws.Cell(row, 1).Value = name;
                ws.Cell(row, 2).Value = count;
                ws.Cell(row, 3).Value = amount;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 4).Value = report.TotalRevenue > 0 ? Math.Round(amount / report.TotalRevenue * 100, 1) : 0;
                ws.Cell(row, 4).Style.NumberFormat.Format = "0.0%";
                row++;
            }

            // Итого
            ws.Cell(row, 1).Value = "ИТОГО";
            ws.Cell(row, 2).Value = report.CashCount + report.CardCount + report.TransferCount + report.QrCount;
            ws.Cell(row, 3).Value = report.CashAmount + report.CardAmount + report.TransferAmount + report.QrAmount;
            ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
            ws.Cell(row, 4).Value = 1.0;
            ws.Cell(row, 4).Style.NumberFormat.Format = "0.0%";
            ws.Range(row, 1, row, 4).Style.Font.Bold = true;

            ws.Columns().AdjustToContents();
        }

        private static void ExportEmployees(XLWorkbook workbook, ShiftReport report)
        {
            var ws = workbook.Worksheets.Add("Сотрудники");

            ws.Cell(1, 1).Value = "Зарплатная ведомость";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            int row = 3;
            WriteTableHeader(ws, row, new[] { "Сотрудник", "Машин", "Выручка", "Начислено", "Авансы", "К выплате" });
            row++;

            foreach (var emp in report.EmployeesWork.OrderByDescending(e => e.Earnings))
            {
                ws.Cell(row, 1).Value = emp.EmployeeName;
                ws.Cell(row, 2).Value = emp.CarsWashed;
                ws.Cell(row, 3).Value = emp.TotalAmount;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 4).Value = emp.Earnings;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 5).Value = emp.Advances;
                ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 6).Value = emp.ToPay;
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 6).Style.Font.Bold = true;
                row++;
            }

            // Итого
            ws.Cell(row, 1).Value = "ИТОГО";
            ws.Cell(row, 2).Value = report.EmployeesWork.Sum(e => e.CarsWashed);
            ws.Cell(row, 3).Value = report.EmployeesWork.Sum(e => e.TotalAmount);
            ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
            ws.Cell(row, 4).Value = report.TotalWasherEarnings;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00 ₽";
            ws.Cell(row, 5).Value = report.TotalAdvances;
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00 ₽";
            ws.Cell(row, 6).Value = report.EmployeesWork.Sum(e => e.ToPay);
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00 ₽";
            ws.Range(row, 1, row, 6).Style.Font.Bold = true;

            ws.Columns().AdjustToContents();
        }

        private static void ExportTopServices(XLWorkbook workbook, ShiftReport report)
        {
            var ws = workbook.Worksheets.Add("Топ услуг");

            ws.Cell(1, 1).Value = "Топ-5 услуг за смену";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            if (!report.TopServices.Any())
            {
                ws.Cell(3, 1).Value = "Нет данных";
                return;
            }

            int row = 3;
            WriteTableHeader(ws, row, new[] { "Услуга", "Количество", "Выручка", "Средняя цена" });
            row++;

            foreach (var svc in report.TopServices.OrderByDescending(s => s.Count))
            {
                ws.Cell(row, 1).Value = svc.ServiceName;
                ws.Cell(row, 2).Value = svc.Count;
                ws.Cell(row, 3).Value = svc.TotalRevenue;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 4).Value = svc.Count > 0 ? svc.TotalRevenue / svc.Count : 0;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00 ₽";
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        private static void ExportExpensesByCategory(XLWorkbook workbook, ShiftReport report)
        {
            var ws = workbook.Worksheets.Add("Расходы по категориям");

            ws.Cell(1, 1).Value = "Разбивка расходов по категориям";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            if (!report.ExpensesByCategory.Any())
            {
                ws.Cell(3, 1).Value = "Нет расходов за смену";
                return;
            }

            int row = 3;
            WriteTableHeader(ws, row, new[] { "Категория", "Транзакций", "Сумма", "% от расходов" });
            row++;

            foreach (var exp in report.ExpensesByCategory.OrderByDescending(e => e.TotalAmount))
            {
                ws.Cell(row, 1).Value = exp.Category;
                ws.Cell(row, 2).Value = exp.Count;
                ws.Cell(row, 3).Value = exp.TotalAmount;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 4).Value = exp.Percentage / 100m;
                ws.Cell(row, 4).Style.NumberFormat.Format = "0.0%";
                row++;
            }

            // Итого
            ws.Cell(row, 1).Value = "ИТОГО";
            ws.Cell(row, 2).Value = report.ExpensesByCategory.Sum(e => e.Count);
            ws.Cell(row, 3).Value = report.TotalExpenses;
            ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
            ws.Cell(row, 4).Value = 1.0;
            ws.Cell(row, 4).Style.NumberFormat.Format = "0.0%";
            ws.Range(row, 1, row, 4).Style.Font.Bold = true;

            ws.Columns().AdjustToContents();
        }

        private static void ExportHourlyLoad(XLWorkbook workbook, ShiftReport report)
        {
            var ws = workbook.Worksheets.Add("Загруженность по часам");

            ws.Cell(1, 1).Value = "Распределение заказов по часам";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            if (!report.HourlyLoad.Any() || report.HourlyLoad.All(h => h.OrdersCount == 0))
            {
                ws.Cell(3, 1).Value = "Нет данных";
                return;
            }

            int row = 3;
            WriteTableHeader(ws, row, new[] { "Час", "Заказов", "Выручка", "Средний чек" });
            row++;

            foreach (var hour in report.HourlyLoad.OrderBy(h => h.Hour))
            {
                if (hour.OrdersCount == 0) continue;

                ws.Cell(row, 1).Value = hour.HourLabel;
                ws.Cell(row, 2).Value = hour.OrdersCount;
                ws.Cell(row, 3).Value = hour.Revenue;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 4).Value = hour.OrdersCount > 0 ? hour.Revenue / hour.OrdersCount : 0;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00 ₽";
                row++;
            }

            // Итого
            ws.Cell(row, 1).Value = "ИТОГО";
            ws.Cell(row, 2).Value = report.HourlyLoad.Sum(h => h.OrdersCount);
            ws.Cell(row, 3).Value = report.HourlyLoad.Sum(h => h.Revenue);
            ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
            ws.Range(row, 1, row, 4).Style.Font.Bold = true;

            ws.Columns().AdjustToContents();
        }

        private static void ExportCashBalance(XLWorkbook workbook, ShiftReport report)
        {
            var ws = workbook.Worksheets.Add("Остаток кассы");

            ws.Cell(1, 1).Value = "Сверка кассы";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            int row = 3;

            ws.Cell(row, 1).Value = "Ожидаемый остаток (по транзакциям):";
            ws.Cell(row, 2).Value = report.ExpectedCashBalance;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00 ₽";
            row++;

            ws.Cell(row, 1).Value = "Фактический остаток (X-отчет):";
            ws.Cell(row, 2).Value = report.ActualCashBalance;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00 ₽";
            row++;

            ws.Cell(row, 1).Value = "Разница:";
            ws.Cell(row, 2).Value = report.CashBalanceDifference;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00 ₽";
            ws.Cell(row, 2).Style.Font.Bold = true;
            ws.Cell(row, 2).Style.Font.FontColor = report.CashBalanceDifference >= 0 ? PositiveColor : NegativeColor;

            ws.Columns().AdjustToContents();
        }

        // ═══════════════════════════════════════════════════════════
        // 2. ЭКСПОРТ ИНТЕРВАЛЬНОГО ОТЧЕТА (Расширенный)
        // ═══════════════════════════════════════════════════════════

        public static void ExportCustomPeriodReport(CustomPeriodReport report, string filePath)
        {
            try
            {
                using (var workbook = new XLWorkbook())
                {
                    // === Лист 1: Сводка ===
                    ExportPeriodSummary(workbook, report);

                    // === Лист 2: По дням ===
                    ExportDailyBreakdown(workbook, report);

                    // === Лист 3: Сотрудники ===
                    ExportPeriodEmployees(workbook, report);

                    // === Лист 4: Топ услуг ===
                    ExportPeriodTopServices(workbook, report);

                    // === Лист 5: Расходы по категориям ===
                    ExportPeriodExpenses(workbook, report);

                    // === Лист 6: Загруженность по часам ===
                    ExportPeriodHourlyLoad(workbook, report);

                    workbook.SaveAs(filePath);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка экспорта в Excel: {ex.Message}");
            }
        }

        private static void ExportPeriodSummary(XLWorkbook workbook, CustomPeriodReport report)
        {
            var ws = workbook.Worksheets.Add("Сводка");

            ws.Cell(1, 1).Value = $"Финансовый отчет: {report.StartDate:dd.MM.yyyy} - {report.EndDate:dd.MM.yyyy}";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Range(1, 1, 1, 6).Merge();

            int row = 3;

            // Основные KPI
            WriteSectionHeader(ws, ref row, "Основные показатели");
            WriteKpiRow(ws, ref row, "Всего машин", report.TotalCars, null);
            WriteKpiRow(ws, ref row, "Общая выручка", report.TotalRevenue, "#,##0.00 ₽");
            WriteKpiRow(ws, ref row, "Средний чек", report.AverageCheck, "#,##0.00 ₽");
            WriteKpiRow(ws, ref row, "Выплаты сотрудникам", report.TotalWasherEarnings, "#,##0.00 ₽");
            WriteKpiRow(ws, ref row, "Доход компании", report.TotalCompanyEarnings, "#,##0.00 ₽");
            WriteKpiRow(ws, ref row, "Расходы", report.TotalExpenses, "-#,##0.00 ₽", true);
            WriteKpiRow(ws, ref row, "Чистая прибыль (ЧПКО)", report.NetProfit, "#,##0.00 ₽", false, true);
            row++;

            // Аналитика по скидкам
            WriteSectionHeader(ws, ref row, "Аналитика по скидкам");
            WriteKpiRow(ws, ref row, "Сумма скидок", report.TotalDiscountAmount, "#,##0.00 ₽");
            WriteKpiRow(ws, ref row, "Заказов со скидкой", report.DiscountedOrdersCount, null);
            row++;

            // Способы оплаты
            WriteSectionHeader(ws, ref row, "Способы оплаты");
            WriteKpiRow(ws, ref row, "💵 Наличные", report.CashAmount, "#,##0.00 ₽");
            WriteKpiRow(ws, ref row, "💳 Карта", report.CardAmount, "#,##0.00 ₽");
            WriteKpiRow(ws, ref row, "📱 Перевод", report.TransferAmount, "#,##0.00 ₽");
            WriteKpiRow(ws, ref row, "🔲 QR-код", report.QrAmount, "#,##0.00 ₽");
            row++;

            // Департаменты
            WriteSectionHeader(ws, ref row, "МОЙКА");
            WriteKpiRow(ws, ref row, "Заказов", report.WashTotalCars, null);
            WriteKpiRow(ws, ref row, "Выручка", report.WashTotalRevenue, "#,##0.00 ₽");
            WriteKpiRow(ws, ref row, "Расходы", report.WashTotalExpenses, "-#,##0.00 ₽", true);
            WriteKpiRow(ws, ref row, "Прибыль", report.WashNetProfit, "#,##0.00 ₽", false, true);
            row++;

            WriteSectionHeader(ws, ref row, "АВТОСЕРВИС");
            WriteKpiRow(ws, ref row, "Заказов", report.ServiceTotalCars, null);
            WriteKpiRow(ws, ref row, "Выручка", report.ServiceTotalRevenue, "#,##0.00 ₽");
            WriteKpiRow(ws, ref row, "Расходы", report.ServiceTotalExpenses, "-#,##0.00 ₽", true);
            WriteKpiRow(ws, ref row, "Прибыль", report.ServiceNetProfit, "#,##0.00 ₽", false, true);

            ws.Columns().AdjustToContents();
        }

        private static void ExportDailyBreakdown(XLWorkbook workbook, CustomPeriodReport report)
        {
            var ws = workbook.Worksheets.Add("По дням");

            ws.Cell(1, 1).Value = "Статистика по дням";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            int row = 3;
            WriteTableHeader(ws, row, new[] { "Дата", "Машин", "Выручка", "Средний чек", "ФОТ", "Доход компании" });
            row++;

            foreach (var day in report.DailyReports.OrderBy(d => d.Date))
            {
                ws.Cell(row, 1).Value = day.Date.ToString("dd.MM.yyyy");
                ws.Cell(row, 2).Value = day.TotalCars;
                ws.Cell(row, 3).Value = day.TotalRevenue;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 4).Value = day.TotalCars > 0 ? day.TotalRevenue / day.TotalCars : 0;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 5).Value = day.TotalWasherEarnings;
                ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 6).Value = day.TotalCompanyEarnings;
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00 ₽";
                row++;
            }

            // Итого
            ws.Cell(row, 1).Value = "ИТОГО";
            ws.Cell(row, 2).Value = report.TotalCars;
            ws.Cell(row, 3).Value = report.TotalRevenue;
            ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
            ws.Cell(row, 4).Value = report.AverageCheck;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00 ₽";
            ws.Cell(row, 5).Value = report.TotalWasherEarnings;
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00 ₽";
            ws.Cell(row, 6).Value = report.TotalCompanyEarnings;
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00 ₽";
            ws.Range(row, 1, row, 6).Style.Font.Bold = true;

            ws.Columns().AdjustToContents();
        }

        private static void ExportPeriodEmployees(XLWorkbook workbook, CustomPeriodReport report)
        {
            var ws = workbook.Worksheets.Add("Сотрудники");

            ws.Cell(1, 1).Value = "Зарплатная ведомость за период";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            int row = 3;
            WriteTableHeader(ws, row, new[] { "Сотрудник", "Машин", "Выручка", "Начислено", "Авансы", "К выплате" });
            row++;

            foreach (var emp in report.EmployeesWork.OrderByDescending(e => e.Earnings))
            {
                ws.Cell(row, 1).Value = emp.EmployeeName;
                ws.Cell(row, 2).Value = emp.CarsWashed;
                ws.Cell(row, 3).Value = emp.TotalAmount;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 4).Value = emp.Earnings;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 5).Value = emp.Advances;
                ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 6).Value = emp.ToPay;
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 6).Style.Font.Bold = true;
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        private static void ExportPeriodTopServices(XLWorkbook workbook, CustomPeriodReport report)
        {
            var ws = workbook.Worksheets.Add("Топ услуг");

            ws.Cell(1, 1).Value = "Топ-5 услуг за период";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            if (!report.TopServices.Any())
            {
                ws.Cell(3, 1).Value = "Нет данных";
                return;
            }

            int row = 3;
            WriteTableHeader(ws, row, new[] { "Услуга", "Количество", "Выручка", "Средняя цена" });
            row++;

            foreach (var svc in report.TopServices.OrderByDescending(s => s.Count))
            {
                ws.Cell(row, 1).Value = svc.ServiceName;
                ws.Cell(row, 2).Value = svc.Count;
                ws.Cell(row, 3).Value = svc.TotalRevenue;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 4).Value = svc.Count > 0 ? svc.TotalRevenue / svc.Count : 0;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00 ₽";
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        private static void ExportPeriodExpenses(XLWorkbook workbook, CustomPeriodReport report)
        {
            var ws = workbook.Worksheets.Add("Расходы по категориям");

            ws.Cell(1, 1).Value = "Разбивка расходов по категориям";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            if (!report.ExpensesByCategory.Any())
            {
                ws.Cell(3, 1).Value = "Нет расходов за период";
                return;
            }

            int row = 3;
            WriteTableHeader(ws, row, new[] { "Категория", "Транзакций", "Сумма", "% от расходов" });
            row++;

            foreach (var exp in report.ExpensesByCategory.OrderByDescending(e => e.TotalAmount))
            {
                ws.Cell(row, 1).Value = exp.Category;
                ws.Cell(row, 2).Value = exp.Count;
                ws.Cell(row, 3).Value = exp.TotalAmount;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 4).Value = exp.Percentage / 100m;
                ws.Cell(row, 4).Style.NumberFormat.Format = "0.0%";
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        private static void ExportPeriodHourlyLoad(XLWorkbook workbook, CustomPeriodReport report)
        {
            var ws = workbook.Worksheets.Add("Загруженность по часам");

            ws.Cell(1, 1).Value = "Распределение заказов по часам";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            if (!report.HourlyLoad.Any() || report.HourlyLoad.All(h => h.OrdersCount == 0))
            {
                ws.Cell(3, 1).Value = "Нет данных";
                return;
            }

            int row = 3;
            WriteTableHeader(ws, row, new[] { "Час", "Заказов", "Выручка", "Средний чек" });
            row++;

            foreach (var hour in report.HourlyLoad.OrderBy(h => h.Hour))
            {
                if (hour.OrdersCount == 0) continue;

                ws.Cell(row, 1).Value = hour.HourLabel;
                ws.Cell(row, 2).Value = hour.OrdersCount;
                ws.Cell(row, 3).Value = hour.Revenue;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 4).Value = hour.OrdersCount > 0 ? hour.Revenue / hour.OrdersCount : 0;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00 ₽";
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        // ═══════════════════════════════════════════════════════════
        // 3. ЭКСПОРТ СРАВНЕНИЯ ПЕРИОДОВ
        // ═══════════════════════════════════════════════════════════

        public static void ExportPeriodComparison(PeriodComparisonFull data, string filePath)
        {
            try
            {
                using (var workbook = new XLWorkbook())
                {
                    // === Лист 1: Сводка с дельтами ===
                    ExportComparisonSummary(workbook, data);

                    // === Лист 2: По дням (два периода) ===
                    ExportComparisonDaily(workbook, data);

                    // === Лист 3: Загруженность по часам ===
                    ExportComparisonHourly(workbook, data);

                    // === Лист 4: Топ услуг (сравнение) ===
                    ExportComparisonServices(workbook, data);

                    // === Лист 5: Расходы (сравнение) ===
                    ExportComparisonExpenses(workbook, data);

                    // === Лист 6: Клиенты ===
                    ExportComparisonClients(workbook, data);

                    // === Лист 7: Департаменты ===
                    ExportComparisonDepartments(workbook, data);

                    // === Лист 8: ФОТ сотрудников ===
                    ExportComparisonFot(workbook, data);

                    workbook.SaveAs(filePath);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка экспорта сравнения в Excel: {ex.Message}");
            }
        }

        private static void ExportComparisonSummary(XLWorkbook workbook, PeriodComparisonFull data)
        {
            var ws = workbook.Worksheets.Add("Сводка");

            ws.Cell(1, 1).Value = "Сравнение периодов";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;

            int row = 3;

            // KPI таблица
            WriteTableHeader(ws, row, new[] { "Показатель", "Прошлый период", "Текущий период", "Изменение", "%" });
            row++;

            var cur = data.CurrentPeriod;
            var prev = data.PreviousPeriod;
            var deltas = data.Deltas;

            WriteComparisonRow(ws, ref row, "Выручка", prev.TotalRevenue, cur.TotalRevenue,
                deltas.RevenueChange, deltas.RevenueChangePercent, "#,##0.00 ₽");
            WriteComparisonRow(ws, ref row, "Чистая прибыль", prev.NetProfit, cur.NetProfit,
                deltas.ProfitChange, deltas.ProfitChangePercent, "#,##0.00 ₽");
            WriteComparisonRow(ws, ref row, "Заказы", prev.TotalCars, cur.TotalCars,
                deltas.CarsChange, deltas.CarsChangePercent, null);
            WriteComparisonRow(ws, ref row, "Средний чек", prev.AverageCheck, cur.AverageCheck,
                deltas.AvgCheckChange, deltas.AvgCheckChangePercent, "#,##0.00 ₽");
            WriteComparisonRow(ws, ref row, "Расходы", prev.TotalExpenses, cur.TotalExpenses,
                deltas.ExpensesChange, deltas.ExpensesChangePercent, "#,##0.00 ₽", true);
            WriteComparisonRow(ws, ref row, "Новые клиенты", prev.NewClientsCount, cur.NewClientsCount,
                deltas.NewClientsChange, deltas.NewClientsChangePercent, null);

            ws.Columns().AdjustToContents();
        }

        private static void ExportComparisonDaily(XLWorkbook workbook, PeriodComparisonFull data)
        {
            var ws = workbook.Worksheets.Add("По дням");

            ws.Cell(1, 1).Value = "Динамика по дням";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            int row = 3;
            WriteTableHeader(ws, row, new[] { "День", "Прошлый - Выручка", "Текущий - Выручка", "Прошлый - Заказы", "Текущий - Заказы" });
            row++;

            int maxDays = Math.Max(data.DailyData.CurrentDays.Count, data.DailyData.PreviousDays.Count);

            for (int i = 0; i < maxDays; i++)
            {
                var prevDay = i < data.DailyData.PreviousDays.Count ? data.DailyData.PreviousDays[i] : null;
                var curDay = i < data.DailyData.CurrentDays.Count ? data.DailyData.CurrentDays[i] : null;

                ws.Cell(row, 1).Value = $"День {i + 1}";

                ws.Cell(row, 2).Value = prevDay?.Revenue ?? 0;
                ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00 ₽";

                ws.Cell(row, 3).Value = curDay?.Revenue ?? 0;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";

                ws.Cell(row, 4).Value = prevDay?.CarsCount ?? 0;
                ws.Cell(row, 5).Value = curDay?.CarsCount ?? 0;

                row++;
            }

            ws.Columns().AdjustToContents();
        }

        private static void ExportComparisonHourly(XLWorkbook workbook, PeriodComparisonFull data)
        {
            var ws = workbook.Worksheets.Add("Загруженность");

            ws.Cell(1, 1).Value = "Загруженность по часам (сравнение)";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            int row = 3;
            WriteTableHeader(ws, row, new[] { "Час", "Прошлый", "Текущий", "Изменение" });
            row++;

            for (int hour = 0; hour < 24; hour++)
            {
                var prevLoad = data.PreviousHourlyLoad.FirstOrDefault(h => h.Hour == hour);
                var curLoad = data.CurrentHourlyLoad.FirstOrDefault(h => h.Hour == hour);

                int prevCount = prevLoad?.OrdersCount ?? 0;
                int curCount = curLoad?.OrdersCount ?? 0;

                ws.Cell(row, 1).Value = $"{hour:D2}:00";
                ws.Cell(row, 2).Value = prevCount;
                ws.Cell(row, 3).Value = curCount;
                ws.Cell(row, 4).Value = curCount - prevCount;
                ws.Cell(row, 4).Style.Font.FontColor = curCount > prevCount ? PositiveColor : curCount < prevCount ? NegativeColor : NeutralColor;

                row++;
            }

            ws.Columns().AdjustToContents();
        }

        private static void ExportComparisonServices(XLWorkbook workbook, PeriodComparisonFull data)
        {
            var ws = workbook.Worksheets.Add("Топ услуг");

            ws.Cell(1, 1).Value = "Сравнение топ-5 услуг";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            if (!data.ServicesComparison.Any())
            {
                ws.Cell(3, 1).Value = "Нет данных";
                return;
            }

            int row = 3;
            WriteTableHeader(ws, row, new[] { "Услуга", "Прошлый (шт)", "Текущий (шт)", "Δ%", "Прошлый (₽)", "Текущий (₽)", "Δ%" });
            row++;

            foreach (var svc in data.ServicesComparison)
            {
                ws.Cell(row, 1).Value = svc.ServiceName;
                ws.Cell(row, 2).Value = svc.PreviousCount;
                ws.Cell(row, 3).Value = svc.CurrentCount;
                ws.Cell(row, 4).Value = svc.CountChangePercent;
                ws.Cell(row, 4).Style.NumberFormat.Format = "0.0";
                ws.Cell(row, 4).Style.Font.FontColor = svc.CountChangePercent > 0 ? PositiveColor : svc.CountChangePercent < 0 ? NegativeColor : NeutralColor;

                ws.Cell(row, 5).Value = svc.PreviousRevenue;
                ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 6).Value = svc.CurrentRevenue;
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 7).Value = svc.RevenueChangePercent;
                ws.Cell(row, 7).Style.NumberFormat.Format = "0.0";
                ws.Cell(row, 7).Style.Font.FontColor = svc.RevenueChangePercent > 0 ? PositiveColor : svc.RevenueChangePercent < 0 ? NegativeColor : NeutralColor;

                row++;
            }

            ws.Columns().AdjustToContents();
        }

        private static void ExportComparisonExpenses(XLWorkbook workbook, PeriodComparisonFull data)
        {
            var ws = workbook.Worksheets.Add("Расходы");

            ws.Cell(1, 1).Value = "Сравнение расходов по категориям";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            if (!data.ExpensesComparison.Any())
            {
                ws.Cell(3, 1).Value = "Нет данных";
                return;
            }

            int row = 3;
            WriteTableHeader(ws, row, new[] { "Категория", "Прошлый (₽)", "Текущий (₽)", "Изменение (₽)", "Δ%" });
            row++;

            foreach (var exp in data.ExpensesComparison)
            {
                ws.Cell(row, 1).Value = exp.Category;
                ws.Cell(row, 2).Value = exp.PreviousAmount;
                ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 3).Value = exp.CurrentAmount;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 4).Value = exp.CurrentAmount - exp.PreviousAmount;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00 ₽";
                ws.Cell(row, 5).Value = exp.AmountChangePercent;
                ws.Cell(row, 5).Style.NumberFormat.Format = "0.0";
                // Для расходов рост = плохо (красный), падение = хорошо (зелёный)
                ws.Cell(row, 5).Style.Font.FontColor = exp.AmountChangePercent > 0 ? NegativeColor : exp.AmountChangePercent < 0 ? PositiveColor : NeutralColor;

                row++;
            }

            ws.Columns().AdjustToContents();
        }

        private static void ExportComparisonClients(XLWorkbook workbook, PeriodComparisonFull data)
        {
            var ws = workbook.Worksheets.Add("Клиенты");

            ws.Cell(1, 1).Value = "Сравнение клиентской статистики";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            int row = 3;
            WriteTableHeader(ws, row, new[] { "Показатель", "Прошлый", "Текущий", "Изменение" });
            row++;

            var cl = data.ClientsComparison;

            ws.Cell(row, 1).Value = "Уникальные клиенты";
            ws.Cell(row, 2).Value = cl.PreviousUnique;
            ws.Cell(row, 3).Value = cl.CurrentUnique;
            ws.Cell(row, 4).Value = cl.CurrentUnique - cl.PreviousUnique;
            row++;

            ws.Cell(row, 1).Value = "Новые клиенты";
            ws.Cell(row, 2).Value = cl.PreviousNew;
            ws.Cell(row, 3).Value = cl.CurrentNew;
            ws.Cell(row, 4).Value = cl.CurrentNew - cl.PreviousNew;
            row++;

            ws.Cell(row, 1).Value = "Повторные клиенты";
            ws.Cell(row, 2).Value = cl.PreviousRepeat;
            ws.Cell(row, 3).Value = cl.CurrentRepeat;
            ws.Cell(row, 4).Value = cl.CurrentRepeat - cl.PreviousRepeat;
            row++;

            ws.Cell(row, 1).Value = "Возвращаемость";
            ws.Cell(row, 2).Value = cl.PreviousRetentionRate;
            ws.Cell(row, 2).Style.NumberFormat.Format = "0.0%";
            ws.Cell(row, 3).Value = cl.CurrentRetentionRate;
            ws.Cell(row, 3).Style.NumberFormat.Format = "0.0%";
            ws.Cell(row, 4).Value = cl.CurrentRetentionRate - cl.PreviousRetentionRate;
            ws.Cell(row, 4).Style.NumberFormat.Format = "0.0";

            ws.Columns().AdjustToContents();
        }

        private static void ExportComparisonDepartments(XLWorkbook workbook, PeriodComparisonFull data)
        {
            var ws = workbook.Worksheets.Add("Департаменты");

            ws.Cell(1, 1).Value = "Сравнение департаментов";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            int row = 3;

            // Мойка
            WriteSectionHeader(ws, ref row, "МОЙКА");
            WriteTableHeader(ws, row, new[] { "Показатель", "Прошлый", "Текущий", "Δ%" });
            row++;

            var wash = data.DepartmentsComparison.Wash;
            WriteDeptComparisonRow(ws, ref row, "Выручка", wash.PreviousRevenue, wash.CurrentRevenue, wash.RevenueChangePercent, "#,##0.00 ₽");
            WriteDeptComparisonRow(ws, ref row, "Заказы", wash.PreviousCars, wash.CurrentCars, wash.CarsChangePercent, null);
            WriteDeptComparisonRow(ws, ref row, "Прибыль", wash.PreviousProfit, wash.CurrentProfit, wash.ProfitChangePercent, "#,##0.00 ₽");

            row += 2;

            // Сервис
            WriteSectionHeader(ws, ref row, "АВТОСЕРВИС");
            WriteTableHeader(ws, row, new[] { "Показатель", "Прошлый", "Текущий", "Δ%" });
            row++;

            var service = data.DepartmentsComparison.Service;
            WriteDeptComparisonRow(ws, ref row, "Выручка", service.PreviousRevenue, service.CurrentRevenue, service.RevenueChangePercent, "#,##0.00 ₽");
            WriteDeptComparisonRow(ws, ref row, "Заказы", service.PreviousCars, service.CurrentCars, service.CarsChangePercent, null);
            WriteDeptComparisonRow(ws, ref row, "Прибыль", service.PreviousProfit, service.CurrentProfit, service.ProfitChangePercent, "#,##0.00 ₽");

            ws.Columns().AdjustToContents();
        }

        private static void ExportComparisonFot(XLWorkbook workbook, PeriodComparisonFull data)
        {
            var ws = workbook.Worksheets.Add("ФОТ сотрудников");

            ws.Cell(1, 1).Value = "Сравнение ФОТ";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            int row = 3;

            // Агрегаты
            WriteSectionHeader(ws, ref row, "Общие показатели");
            WriteTableHeader(ws, row, new[] { "Показатель", "Прошлый", "Текущий", "Δ%" });
            row++;

            var fot = data.FotComparison;
            WriteDeptComparisonRow(ws, ref row, "Общий ФОТ", fot.PreviousTotalFot, fot.CurrentTotalFot, fot.TotalFotChangePercent, "#,##0.00 ₽");
            WriteDeptComparisonRow(ws, ref row, "Средний на сотрудника", fot.PreviousAvgPerEmployee, fot.CurrentAvgPerEmployee, 0, "#,##0.00 ₽");
            WriteDeptComparisonRow(ws, ref row, "Сотрудников", fot.PreviousEmployeesCount, fot.CurrentEmployeesCount, 0, null);

            row += 2;

            // Таблица по сотрудникам
            WriteSectionHeader(ws, ref row, "По сотрудникам");
            WriteTableHeader(ws, row, new[] { "Сотрудник", "Прошлый (₽)", "Текущий (₽)", "Δ%", "Заказы (тек)", "Авансы (тек)" });
            row++;

            foreach (var emp in data.FotComparison.Employees.OrderByDescending(e => e.CurrentEarnings))
            {
                ws.Cell(row, 1).Value = emp.EmployeeName;

                ws.Cell(row, 2).Value = emp.PreviousEarnings;
                ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00 ₽";

                ws.Cell(row, 3).Value = emp.CurrentEarnings;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";

                ws.Cell(row, 4).Value = emp.EarningsChangePercent;
                ws.Cell(row, 4).Style.NumberFormat.Format = "0.0";
                ws.Cell(row, 4).Style.Font.FontColor = emp.EarningsChangePercent > 0 ? PositiveColor : emp.EarningsChangePercent < 0 ? NegativeColor : NeutralColor;

                ws.Cell(row, 5).Value = emp.CurrentCars;

                ws.Cell(row, 6).Value = emp.CurrentAdvances;
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00 ₽";

                row++;
            }

            ws.Columns().AdjustToContents();
        }

        // ═══════════════════════════════════════════════════════════
        //  ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ═══════════════════════════════════════════════════════════

        private static void WriteSectionHeader(IXLWorksheet ws, ref int row, string title)
        {
            ws.Cell(row, 1).Value = title;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = HeaderBg;
            row++;
        }

        private static void WriteTableHeader(IXLWorksheet ws, int row, string[] headers)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(row, i + 1).Value = headers[i];
                ws.Cell(row, i + 1).Style.Font.Bold = true;
                ws.Cell(row, i + 1).Style.Fill.BackgroundColor = HeaderBg;
            }
        }

        private static void WriteKpiRow(IXLWorksheet ws, ref int row, string label, decimal value, string format, bool isExpense = false, bool isProfit = false)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 2).Value = value;

            if (!string.IsNullOrEmpty(format))
            {
                ws.Cell(row, 2).Style.NumberFormat.Format = format;
            }

            if (isExpense)
            {
                ws.Cell(row, 2).Style.Font.FontColor = NegativeColor;
            }
            else if (isProfit)
            {
                ws.Cell(row, 2).Style.Font.FontColor = PositiveColor;
                ws.Cell(row, 2).Style.Font.Bold = true;
            }

            row++;
        }

        private static void WriteComparisonRow(IXLWorksheet ws, ref int row, string label, decimal prev, decimal cur, decimal change, decimal changePercent, string format, bool isExpense = false)
        {
            ws.Cell(row, 1).Value = label;

            ws.Cell(row, 2).Value = prev;
            if (!string.IsNullOrEmpty(format)) ws.Cell(row, 2).Style.NumberFormat.Format = format;

            ws.Cell(row, 3).Value = cur;
            if (!string.IsNullOrEmpty(format)) ws.Cell(row, 3).Style.NumberFormat.Format = format;

            ws.Cell(row, 4).Value = change;
            if (!string.IsNullOrEmpty(format)) ws.Cell(row, 4).Style.NumberFormat.Format = format;

            ws.Cell(row, 5).Value = changePercent;
            ws.Cell(row, 5).Style.NumberFormat.Format = "0.0";

            // Для расходов: рост = плохо (красный), падение = хорошо (зелёный)
            // Для доходов: рост = хорошо (зелёный), падение = плохо (красный)
            if (isExpense)
            {
                ws.Cell(row, 5).Style.Font.FontColor = changePercent > 0 ? NegativeColor : changePercent < 0 ? PositiveColor : NeutralColor;
            }
            else
            {
                ws.Cell(row, 5).Style.Font.FontColor = changePercent > 0 ? PositiveColor : changePercent < 0 ? NegativeColor : NeutralColor;
            }

            row++;
        }

        private static void WriteDeptComparisonRow(IXLWorksheet ws, ref int row, string label, decimal prev, decimal cur, decimal changePercent, string format)
        {
            ws.Cell(row, 1).Value = label;

            ws.Cell(row, 2).Value = prev;
            if (!string.IsNullOrEmpty(format)) ws.Cell(row, 2).Style.NumberFormat.Format = format;

            ws.Cell(row, 3).Value = cur;
            if (!string.IsNullOrEmpty(format)) ws.Cell(row, 3).Style.NumberFormat.Format = format;

            ws.Cell(row, 4).Value = changePercent;
            ws.Cell(row, 4).Style.NumberFormat.Format = "0.0";
            ws.Cell(row, 4).Style.Font.FontColor = changePercent > 0 ? PositiveColor : changePercent < 0 ? NegativeColor : NeutralColor;

            row++;
        }
    }
}