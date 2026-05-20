using ClosedXML.Excel;
using AccuratSystem.Contracts.Models;
using System;
using System.Linq;

namespace AccuratPanelCarWashing
{
    public static class ExcelExporter
    {
        // =========================================================
        // 1. ЭКСПОРТ ЕЖЕДНЕВНОЙ СМЕНЫ
        // =========================================================
        public static void ExportShiftReport(ShiftReport report, string filePath)
        {
            try
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Отчет о смене");

                    // Заголовок
                    worksheet.Cell(1, 1).Value = $"Отчет о смене от {report.Date:dd.MM.yyyy}";
                    worksheet.Cell(1, 1).Style.Font.Bold = true;
                    worksheet.Cell(1, 1).Style.Font.FontSize = 16;
                    worksheet.Range(1, 1, 1, 6).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Основная информация
                    int row = 3;
                    worksheet.Cell(row, 1).Value = "Дата:";
                    worksheet.Cell(row, 2).Value = report.Date.ToString("dd.MM.yyyy");
                    row++;

                    worksheet.Cell(row, 1).Value = "Время начала:";
                    worksheet.Cell(row, 2).Value = report.StartTime.ToString("HH:mm:ss");
                    row++;

                    worksheet.Cell(row, 1).Value = "Время окончания:";
                    worksheet.Cell(row, 2).Value = report.EndTime.HasValue ? report.EndTime.Value.ToString("HH:mm:ss") : "Не закрыта";
                    row++;

                    worksheet.Cell(row, 1).Value = "Всего машин:";
                    worksheet.Cell(row, 2).Value = report.TotalCars;
                    row++;

                    worksheet.Cell(row, 1).Value = "Общая выручка:";
                    worksheet.Cell(row, 2).Value = report.TotalRevenue;
                    worksheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00 ₽";
                    row++;

                    worksheet.Cell(row, 1).Value = "Выплаты мойщикам (Начислено):";
                    worksheet.Cell(row, 2).Value = report.TotalWasherEarnings;
                    worksheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00 ₽";
                    row++;

                    worksheet.Cell(row, 1).Value = "Доход компании (Грязными):";
                    worksheet.Cell(row, 2).Value = report.TotalCompanyEarnings;
                    worksheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00 ₽";
                    row++;

                    worksheet.Cell(row, 1).Value = "Расходы (Химия, нужды):";
                    worksheet.Cell(row, 2).Value = report.TotalExpenses;
                    worksheet.Cell(row, 2).Style.Font.FontColor = XLColor.DarkRed;
                    worksheet.Cell(row, 2).Style.NumberFormat.Format = "-#,##0.00 ₽";
                    row++;

                    worksheet.Cell(row, 1).Value = "Чистая прибыль (ЧПКО):";
                    worksheet.Cell(row, 2).Value = report.NetProfit;
                    worksheet.Cell(row, 2).Style.Font.FontColor = XLColor.Green;
                    worksheet.Cell(row, 2).Style.Font.Bold = true;
                    worksheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00 ₽";
                    row += 2;

                    // Статистика по способам оплаты
                    worksheet.Cell(row, 1).Value = "СТАТИСТИКА ПО СПОСОБАМ ОПЛАТЫ";
                    worksheet.Range(row, 1, row, 3).Merge().Style.Font.Bold = true;
                    worksheet.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.LightGray;
                    row++;

                    worksheet.Cell(row, 1).Value = "Способ оплаты";
                    worksheet.Cell(row, 2).Value = "Количество";
                    worksheet.Cell(row, 3).Value = "Сумма";
                    worksheet.Range(row, 1, row, 3).Style.Font.Bold = true;
                    row++;

                    worksheet.Cell(row, 1).Value = "💵 Наличные";
                    worksheet.Cell(row, 2).Value = report.CashCount;
                    worksheet.Cell(row, 3).Value = report.CashAmount;
                    worksheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
                    row++;

                    worksheet.Cell(row, 1).Value = "💳 Карта";
                    worksheet.Cell(row, 2).Value = report.CardCount;
                    worksheet.Cell(row, 3).Value = report.CardAmount;
                    worksheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
                    row++;

                    worksheet.Cell(row, 1).Value = "📱 Перевод";
                    worksheet.Cell(row, 2).Value = report.TransferCount;
                    worksheet.Cell(row, 3).Value = report.TransferAmount;
                    worksheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
                    row++;

                    worksheet.Cell(row, 1).Value = "📱 QR-код";
                    worksheet.Cell(row, 2).Value = report.QrCount;
                    worksheet.Cell(row, 3).Value = report.QrAmount;
                    worksheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
                    row++;

                    worksheet.Cell(row, 1).Value = "ИТОГО:";
                    worksheet.Cell(row, 2).Value = report.CashCount + report.CardCount + report.TransferCount + report.QrCount;
                    worksheet.Cell(row, 3).Value = report.CashAmount + report.CardAmount + report.TransferAmount + report.QrAmount;
                    worksheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";
                    worksheet.Range(row, 1, row, 3).Style.Font.Bold = true;
                    row += 2;

                    // Заголовок таблицы сотрудников
                    worksheet.Cell(row, 1).Value = "Сотрудник";
                    worksheet.Cell(row, 2).Value = "Машин";
                    worksheet.Cell(row, 3).Value = "Выручка (Грязными)";
                    worksheet.Cell(row, 4).Value = "Начислено (ЗП)";
                    worksheet.Cell(row, 5).Value = "Выдано авансов";
                    worksheet.Cell(row, 6).Value = "К ВЫПЛАТЕ";
                    worksheet.Range(row, 1, row, 6).Style.Font.Bold = true;
                    worksheet.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.LightGray;
                    row++;

                    // Данные сотрудников
                    foreach (var emp in report.EmployeesWork.OrderByDescending(e => e.CarsWashed))
                    {
                        worksheet.Cell(row, 1).Value = emp.EmployeeName;
                        worksheet.Cell(row, 2).Value = emp.CarsWashed;

                        worksheet.Cell(row, 3).Value = emp.TotalAmount;
                        worksheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";

                        worksheet.Cell(row, 4).Value = emp.Earnings;
                        worksheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00 ₽";

                        worksheet.Cell(row, 5).Value = emp.Advances;
                        worksheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00 ₽";

                        worksheet.Cell(row, 6).Value = emp.ToPay;
                        worksheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00 ₽";
                        worksheet.Cell(row, 6).Style.Font.Bold = true;
                        row++;
                    }

                    // Примечание
                    if (!string.IsNullOrEmpty(report.Notes))
                    {
                        row++;
                        worksheet.Cell(row, 1).Value = "Примечание:";
                        worksheet.Cell(row, 2).Value = report.Notes;
                    }

                    // Автоширина колонок
                    worksheet.Columns().AdjustToContents();

                    workbook.SaveAs(filePath);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка экспорта в Excel: {ex.Message}");
            }
        }

        // =========================================================
        // 2. ЭКСПОРТ ИНТЕРВАЛЬНОГО ОТЧЕТА (ВЗАМЕН МЕСЯЧНОМУ)
        // =========================================================
        public static void ExportCustomPeriodReport(CustomPeriodReport report, string filePath)
        {
            try
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add($"Отчет {report.StartDate:dd.MM} - {report.EndDate:dd.MM}");

                    // Заголовок
                    worksheet.Cell(1, 1).Value = $"Финансовый отчет за период: {report.StartDate:dd.MM.yyyy} - {report.EndDate:dd.MM.yyyy}";
                    worksheet.Cell(1, 1).Style.Font.Bold = true;
                    worksheet.Cell(1, 1).Style.Font.FontSize = 16;
                    worksheet.Range(1, 1, 1, 6).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Итоги
                    int row = 3;
                    worksheet.Cell(row, 1).Value = "Всего машин за период:";
                    worksheet.Cell(row, 2).Value = report.TotalCars;
                    row++;

                    worksheet.Cell(row, 1).Value = "Общая выручка:";
                    worksheet.Cell(row, 2).Value = report.TotalRevenue;
                    worksheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00 ₽";
                    row++;

                    worksheet.Cell(row, 1).Value = "Выплаты мойщикам (Начислено):";
                    worksheet.Cell(row, 2).Value = report.TotalWasherEarnings;
                    worksheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00 ₽";
                    row++;

                    worksheet.Cell(row, 1).Value = "Доход компании (Грязными):";
                    worksheet.Cell(row, 2).Value = report.TotalCompanyEarnings;
                    worksheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00 ₽";
                    row++;

                    worksheet.Cell(row, 1).Value = "Расходы за период (Химия и т.д.):";
                    worksheet.Cell(row, 2).Value = report.TotalExpenses;
                    worksheet.Cell(row, 2).Style.Font.FontColor = XLColor.DarkRed;
                    worksheet.Cell(row, 2).Style.NumberFormat.Format = "-#,##0.00 ₽";
                    row++;

                    worksheet.Cell(row, 1).Value = "Чистая прибыль (ЧПКО):";
                    worksheet.Cell(row, 2).Value = report.NetProfit;
                    worksheet.Cell(row, 2).Style.Font.Bold = true;
                    worksheet.Cell(row, 2).Style.Font.FontColor = XLColor.Green;
                    worksheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00 ₽";
                    row += 2;

                    // Заголовок таблицы по дням
                    worksheet.Cell(row, 1).Value = "СТАТИСТИКА ПО ДНЯМ";
                    worksheet.Range(row, 1, row, 5).Merge().Style.Font.Bold = true;
                    worksheet.Range(row, 1, row, 5).Style.Fill.BackgroundColor = XLColor.LightGray;
                    row++;

                    worksheet.Cell(row, 1).Value = "Дата";
                    worksheet.Cell(row, 2).Value = "Машин";
                    worksheet.Cell(row, 3).Value = "Выручка";
                    worksheet.Cell(row, 4).Value = "Начислено (ЗП)";
                    worksheet.Cell(row, 5).Value = "Компании (Грязными)";
                    worksheet.Range(row, 1, row, 5).Style.Font.Bold = true;
                    row++;

                    // Данные по дням
                    foreach (var day in report.DailyReports.OrderBy(d => d.Date))
                    {
                        worksheet.Cell(row, 1).Value = day.Date.ToString("dd.MM.yyyy");
                        worksheet.Cell(row, 2).Value = day.TotalCars;

                        worksheet.Cell(row, 3).Value = day.TotalRevenue;
                        worksheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";

                        worksheet.Cell(row, 4).Value = day.TotalWasherEarnings;
                        worksheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00 ₽";

                        worksheet.Cell(row, 5).Value = day.TotalCompanyEarnings;
                        worksheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00 ₽";
                        row++;
                    }

                    row += 2;

                    // Таблица по сотрудникам (Зарплатная ведомость)
                    worksheet.Cell(row, 1).Value = "ЗАРПЛАТНАЯ ВЕДОМОСТЬ (СВОДНАЯ)";
                    worksheet.Range(row, 1, row, 6).Merge().Style.Font.Bold = true;
                    worksheet.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.LightGray;
                    row++;

                    worksheet.Cell(row, 1).Value = "Сотрудник";
                    worksheet.Cell(row, 2).Value = "Всего машин";
                    worksheet.Cell(row, 3).Value = "Выручка (Грязными)";
                    worksheet.Cell(row, 4).Value = "Начислено (ЗП)";
                    worksheet.Cell(row, 5).Value = "Уже выдано (Авансы)";
                    worksheet.Cell(row, 6).Value = "К ВЫПЛАТЕ";
                    worksheet.Range(row, 1, row, 6).Style.Font.Bold = true;
                    row++;

                    foreach (var emp in report.EmployeesWork.OrderByDescending(e => e.Earnings))
                    {
                        worksheet.Cell(row, 1).Value = emp.EmployeeName;
                        worksheet.Cell(row, 2).Value = emp.CarsWashed;

                        worksheet.Cell(row, 3).Value = emp.TotalAmount;
                        worksheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00 ₽";

                        worksheet.Cell(row, 4).Value = emp.Earnings;
                        worksheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00 ₽";

                        worksheet.Cell(row, 5).Value = emp.Advances;
                        worksheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00 ₽";

                        worksheet.Cell(row, 6).Value = emp.ToPay;
                        worksheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00 ₽";
                        worksheet.Cell(row, 6).Style.Font.Bold = true;
                        row++;
                    }

                    // Автоширина колонок
                    worksheet.Columns().AdjustToContents();

                    workbook.SaveAs(filePath);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка экспорта в Excel: {ex.Message}");
            }
        }
    }
}
