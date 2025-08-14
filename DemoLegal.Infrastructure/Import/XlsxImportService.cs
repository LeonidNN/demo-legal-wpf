using System.Globalization;
using System.Text.Json;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DemoLegal.Domain.Models;
using DemoLegal.Infrastructure.Database;
using DemoLegal.Infrastructure.Repositories;

namespace DemoLegal.Infrastructure.Import;

public sealed class XlsxImportService
{
    private readonly IAccountRepository _accounts = new AccountRepository();
    private readonly IPeriodBalanceRepository _balances = new PeriodBalanceRepository();
    private readonly ICaseFileRepository _cases = new CaseFileRepository();

    public async Task<ImportSummary> ImportXlsxAsync(string path, int? rowLimit = null)
    {
        SqliteConnectionFactory.Configure();
        var summary = new ImportSummary();

        using var doc = SpreadsheetDocument.Open(path, false);
        var wbPart = doc.WorkbookPart ?? throw new InvalidOperationException("Некорректный XLSX: нет WorkbookPart");
        var sheet = wbPart.Workbook.Sheets!.Elements<Sheet>().FirstOrDefault()
                    ?? throw new InvalidOperationException("В XLSX нет листов");
        var wsPart = (WorksheetPart)(wbPart.GetPartById(sheet.Id!));
        var sst = wbPart.SharedStringTablePart?.SharedStringTable;

        // Все строки листа
        var sheetData = wsPart.Worksheet.GetFirstChild<SheetData>()
                        ?? throw new InvalidOperationException("Нет SheetData в листе");
        var rows = sheetData.Elements<Row>().ToList();
        if (rows.Count == 0) return summary;

        // Заголовок (первая непустая строка)
        var headerRow = rows.FirstOrDefault(r => r.Elements<Cell>().Any()) ?? rows[0];
        var headerIndex = rows.IndexOf(headerRow);

        // Считываем названия колонок по фактическим позициям (CellReference)
        var headers = ReadRowStrings(headerRow, sst);
        var hmap = BuildHeaderMap(headers);

        // Основной цикл
        for (int i = headerIndex + 1; i < rows.Count; i++)
        {
            if (rowLimit.HasValue && summary.RowsRead >= rowLimit.Value) break;
            var r = rows[i];

            var values = ReadRowStrings(r, sst);
            string Get(string name) => GetByNames(values, hmap, new[] { name }) ?? string.Empty;
            string? GetOpt(string name) => GetByNames(values, hmap, new[] { name });

            summary.RowsRead++;

            // Найти ЛС по синонимам
            var ls = GetByNames(values, hmap, new[]
            {
                "ЛС","Л/С","Л. С.","Л С","ЛC","Лицевой счёт","Лицевой счет","Лицевой счет (ЛС)"
            });

            if (string.IsNullOrWhiteSpace(ls))
            {
                summary.Warnings.Add($"Строка {summary.RowsRead}: пустой ЛС — пропуск.");
                continue;
            }

            var org     = GetByNames(values, hmap, new[] { "Организация" }) ?? "Организация";
            var address = GetByNames(values, hmap, new[] { "Адрес" }) ?? string.Empty;
            var periodStr = GetByNames(values, hmap, new[] { "Период" }) ?? string.Empty;
            var period = ParsePeriod(periodStr);

            decimal Debt(string col)
            {
                var raw = (GetByNames(values, hmap, new[] { col }) ?? string.Empty)
                    .Replace(" ", "").Replace("\u00A0", "").Replace(",", ".");
                return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;
            }

            var debtStart = Debt("Задолженность на начало");
            var accrued   = Debt("Начислено");
            var paidRaw   = GetByNames(values, hmap, new[] { "Оплачено" });
            var paid      = string.IsNullOrWhiteSpace(paidRaw) ? 0m : Debt("Оплачено");
            var debtEnd   = Debt("Задолженность на конец");

            // Баланс
            var diff = Math.Abs((double)((debtStart + accrued - paid) - debtEnd));
            if (diff > 0.01)
            {
                summary.BalanceMismatches++;
                summary.Warnings.Add($"ЛС {ls} {period:yyyy-MM}: баланс не сходится (+/- {diff:0.00}). Строка пропущена.");
                continue;
            }

            // Account
            var acc = new Account
            {
                Ls = ls,
                LsCode = GetOpt("Код ЛС"),
                Fio = GetOpt("ФИО"),
                AddressRaw = address,
                AddressNorm = null,
                PremisesType = GetOpt("Тип помещения"),
                LsStatus = GetOpt("Статус ЛС"),
                LsCloseDate = ParseNullableDate(GetOpt("Дата закрытия ЛС")),
                LsType = GetOpt("Тип ЛС"),
                MgmtStatus = GetOpt("Статус управления домом"),
                Organization = org,
                GroupCompany = GetOpt("ГК"),
                Division = GetOpt("Дивизионы"),
                DivisionHead = GetOpt("Руководитель дивизиона"),
                AccrualCenter = GetOpt("Центр начислений"),
                ObjectName = GetOpt("Объект"),
                District = GetOpt("Район"),
                House = GetOpt("Дом"),
                AdrN = GetOpt("АдрН")
            };

            int accountId;
            try
            {
                accountId = await _accounts.GetOrCreateAsync(acc);
            }
            catch (Exception ex)
            {
                summary.Warnings.Add($"ЛС {acc.Ls}: ошибка сохранения аккаунта: {ex.Message} — пропуск строки.");
                continue;
            }

            // PeriodBalance
            int? months = ParseNullableInt(GetOpt("Месяцы задолженности") ?? GetOpt(" Месяцы задолженности"));
            var pb = new PeriodBalance
            {
                AccountId = accountId,
                PeriodDate = period,
                DebtStart = debtStart,
                Accrued = accrued,
                Paid = paid,
                DebtEnd = debtEnd,
                MonthsInDebt = months,
                DebtCategory = GetOpt("Категория долга"),
                DebtStructure = GetOpt("Структура долга"),
                SrcFile = GetOpt("Файл"),
                RoomNo = GetOpt("№скв")
            };
            await _balances.UpsertAsync(pb);

            // CaseFile по последнему периоду
            var latest = await _balances.GetLatestByAccountAsync(accountId);
            if (latest is not null)
            {
                var (periodFrom, periodTo) = CalcPeriodRange(latest.PeriodDate, latest.MonthsInDebt);
                var debtorType = (acc.LsType ?? "").Trim().Equals("Распределенные", StringComparison.OrdinalIgnoreCase)
                                 ? "person" : "company";

                var mgmtText = BuildMgmtStatusText(acc, periodTo);
                var flags = new Dictionary<string, bool>();
                if (debtorType == "person")
                {
                    flags["need_birth_date"] = true;
                    flags["need_birth_place"] = true;
                }
                else
                {
                    flags["need_inn"] = true;
                }

                var cf = new CaseFile
                {
                    AccountId = accountId,
                    CreatedAt = DateTime.UtcNow,
                    Status = "candidate",
                    DebtorType = debtorType,
                    DebtAmount = latest.DebtEnd,
                    PeriodFrom = periodFrom,
                    PeriodTo = periodTo,
                    ServiceKind = "ЖКУ (обобщ.)",
                    MgmtStatusText = mgmtText,
                    EnrichmentFlagsJson = JsonSerializer.Serialize(flags)
                };
                await _cases.UpsertAsync(cf);
            }

            summary.RowsImported++;
        }

        return summary;
    }

    // ===== Helpers =====

    private static List<string> ReadRowStrings(Row row, SharedStringTable? sst)
    {
        // Достаём пары: ColumnIndex -> string
        var dict = new Dictionary<int, string>();
        foreach (var c in row.Elements<Cell>())
        {
            var idx = GetColumnIndex(c.CellReference!.Value);
            var val = GetCellString(c, sst);
            dict[idx] = val;
        }
        // Преобразуем в плотный список от 0..max
        var res = new List<string>();
        for (int i = 0; i <= (dict.Count == 0 ? -1 : dict.Keys.Max()); i++)
        {
            res.Add(dict.TryGetValue(i, out var v) ? v : string.Empty);
        }
        return res;
    }

    private static int GetColumnIndex(string? cellRef)
    {
        if (string.IsNullOrWhiteSpace(cellRef)) return -1;
// Преобразуем буквенную часть (A, B, ..., AA, AB, ...) в 0-based индекс
        int col = 0;
        foreach (var ch in cellRef)
        {
            if (char.IsLetter(ch))
            {
                col = col * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
            }
            else break;
        }
        return col - 1; // 0-based
    }

    private static Dictionary<string, int> BuildHeaderMap(List<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
        {
            var key = Norm(headers[i]);
            if (!map.ContainsKey(key))
                map[key] = i;
        }
        return map;
    }

    private static string? GetByNames(List<string> values, Dictionary<string,int> hmap, string[] names)
    {
        foreach (var name in names)
        {
            var key = Norm(name);
            if (!hmap.TryGetValue(key, out var idx)) continue;
            if (idx < 0 || idx >= values.Count) continue;
            var val = values[idx];
            if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
        }
        return null;
    }

    private static string GetCellString(Cell cell, SharedStringTable? sst)
    {
        if (cell.DataType != null && cell.DataType == CellValues.SharedString)
        {
            if (int.TryParse(cell.CellValue?.Text, out var idx) && sst != null)
                return sst.ElementAt(idx).InnerText;
        }
        return cell.CellValue?.InnerText ?? string.Empty;
    }

    private static string Norm(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var t = s.Replace("\u00A0"," ").Trim();
        while (t.Contains("  ")) t = t.Replace("  "," ");
        return t;
    }

    private static DateOnly ParsePeriod(string raw)
    {
        if (DateTime.TryParse(raw, new CultureInfo("ru-RU"), DateTimeStyles.None, out var dt))
            return new DateOnly(dt.Year, dt.Month, 1);
        var now = DateTime.Now;
        return new DateOnly(now.Year, now.Month, 1);
    }

    private static DateTime? ParseNullableDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (DateTime.TryParse(raw, new CultureInfo("ru-RU"), DateTimeStyles.None, out var dt))
            return dt.Date;
        return null;
    }

    private static int? ParseNullableInt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return int.TryParse(raw, out var n) ? n : null;
    }

    private static (DateOnly from, DateOnly to) CalcPeriodRange(DateOnly periodTo, int? monthsInDebt)
    {
        if (monthsInDebt is int m && m > 0)
        {
            var start = periodTo.AddMonths(-(m - 1));
            return (start, periodTo);
        }
        return (periodTo, periodTo);
    }

    private static string BuildMgmtStatusText(Account acc, DateOnly periodTo)
    {
        var isActive = string.Equals(acc.LsStatus, "Действующий", StringComparison.OrdinalIgnoreCase);
        var inLicense = (acc.MgmtStatus ?? string.Empty).Contains("Управление (в лицензии)", StringComparison.OrdinalIgnoreCase);
        if (isActive && inLicense)
            return $"Дом находится под управлением {acc.Organization} (в лицензии).";

        var endDate = acc.LsCloseDate?.Date
            ?? new DateTime(periodTo.Year, periodTo.Month, DateTime.DaysInMonth(periodTo.Year, periodTo.Month));
        return $"До {endDate:dd.MM.yyyy} управляющая организация {acc.Organization} управляла домом.";
    }
}

