using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DemoLegal.Application.Abstractions;
using DemoLegal.Application.DTOs;
using DemoLegal.Domain.Entities;
using DemoLegal.Infrastructure.Import.Models;
using DemoLegal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;

namespace DemoLegal.Infrastructure.Import;

/// <summary>
/// Импорт XLSX (первая таблица/лист). Ожидается шапка колонок как в ExpectedColumns.
/// </summary>
public sealed class XlsxImporter : IImporter
{
    private readonly DemoContext _db;
    public XlsxImporter(DemoContext db) => _db = db;

    public async Task<ImportReportDto> ImportAsync(string path)
    {
        if (!System.IO.File.Exists(path))
            return new ImportReportDto(0, 0, 1, new[] { $"Файл не найден: {path}" });

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.FirstOrDefault();
        if (ws is null) return new ImportReportDto(0, 0, 1, new[] { "В книге нет листов." });

        // читаем шапку
        var firstRowUsed = ws.FirstRowUsed();
        var headerRow = firstRowUsed ?? ws.Row(1);
        var headerCells = headerRow.CellsUsed().ToList();
        if (headerCells.Count == 0) return new ImportReportDto(0, 0, 1, new[] { "Не найдена строка заголовка." });

        var header = headerCells.Select(c => c.GetString().Trim()).ToList();
        var map = BuildColumnMap(header);

        var ru = new CultureInfo("ru-RU");
        int rowsRead = 0, rowsImported = 0, errors = 0;
        var messages = new List<string>();
        var accCache = new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase);

        // строки данных начинаются со следующей после заголовка
        var startRow = headerRow.RowNumber() + 1;
        var lastRowUsed = ws.LastRowUsed()?.RowNumber() ?? startRow - 1
New-Item -ItemType File src\DemoLegal.Infrastructure\Import\XlsxImporter.cs -Force | Out-Null
Set-Content -Path src\DemoLegal.Infrastructure\Import\XlsxImporter.cs -Encoding UTF8 -Value @'
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DemoLegal.Application.Abstractions;
using DemoLegal.Application.DTOs;
using DemoLegal.Domain.Entities;
using DemoLegal.Infrastructure.Import.Models;
using DemoLegal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;

namespace DemoLegal.Infrastructure.Import;

/// <summary>
/// Импорт XLSX (первая таблица/лист). Ожидается шапка колонок как в ExpectedColumns.
/// </summary>
public sealed class XlsxImporter : IImporter
{
    private readonly DemoContext _db;
    public XlsxImporter(DemoContext db) => _db = db;

    public async Task<ImportReportDto> ImportAsync(string path)
    {
        if (!System.IO.File.Exists(path))
            return new ImportReportDto(0, 0, 1, new[] { $"Файл не найден: {path}" });

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.FirstOrDefault();
        if (ws is null) return new ImportReportDto(0, 0, 1, new[] { "В книге нет листов." });

        // читаем шапку
        var firstRowUsed = ws.FirstRowUsed();
        var headerRow = firstRowUsed ?? ws.Row(1);
        var headerCells = headerRow.CellsUsed().ToList();
        if (headerCells.Count == 0) return new ImportReportDto(0, 0, 1, new[] { "Не найдена строка заголовка." });

        var header = headerCells.Select(c => c.GetString().Trim()).ToList();
        var map = BuildColumnMap(header);

        var ru = new CultureInfo("ru-RU");
        int rowsRead = 0, rowsImported = 0, errors = 0;
        var messages = new List<string>();
        var accCache = new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase);

        // строки данных начинаются со следующей после заголовка
        var startRow = headerRow.RowNumber() + 1;
        var lastRowUsed = ws.LastRowUsed()?.RowNumber() ?? startRow - 1;

        for (int r = startRow; r <= lastRowUsed; r++)
        {
            var row = ws.Row(r);
            if (row.IsEmpty()) continue;

            rowsRead++;

            string GetCell(string col)
            {
                if (!map.TryGetValue(col, out var idx)) return string.Empty;
                var cell = row.Cell(idx + 1); // индекс +1, т.к. map по списку шапки (0..n-1)
                if (cell.DataType == XLDataType.Number && col is ExpectedColumns.Period or ExpectedColumns.LsCloseDate)
                {
                    // даты в Excel как числа
                    try { return cell.GetDateTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture); }
                    catch { /* fallback ниже */ }
                }
                return cell.GetFormattedString().Trim();
            }

            var raw = new RawRow
            {
                File = GetCell(ExpectedColumns.File),
                RoomNo = GetCell(ExpectedColumns.RoomNo),
                GroupCompany = GetCell(ExpectedColumns.GroupCompany),
                Organization = GetCell(ExpectedColumns.Organization),
                House = GetCell(ExpectedColumns.House),
                Address = GetCell(ExpectedColumns.Address),
                Ls = GetCell(ExpectedColumns.Ls),
                LsCode = GetCell(ExpectedColumns.LsCode),
                Fio = GetCell(ExpectedColumns.Fio),
                DebtStart = GetCell(ExpectedColumns.DebtStart),
                Accrued = GetCell(ExpectedColumns.Accrued),
                Paid = GetCell(ExpectedColumns.Paid),
                DebtEnd = GetCell(ExpectedColumns.DebtEnd),
                DebtStructure = GetCell(ExpectedColumns.DebtStructure),
                MonthsInDebt = GetCell(ExpectedColumns.MonthsInDebt),
                DebtCategory = GetCell(ExpectedColumns.DebtCategory),
                MgmtStatus = GetCell(ExpectedColumns.MgmtStatus),
                District = GetCell(ExpectedColumns.District),
                ObjectName = GetCell(ExpectedColumns.ObjectName),
                Division = GetCell(ExpectedColumns.Division),
                DivisionHead = GetCell(ExpectedColumns.DivisionHead),
                PremisesType = GetCell(ExpectedColumns.PremisesType),
                LsStatus = GetCell(ExpectedColumns.LsStatus),
                LsCloseDate = GetCell(ExpectedColumns.LsCloseDate),
                LsType = GetCell(ExpectedColumns.LsType),
                AccrualCenter = GetCell(ExpectedColumns.AccrualCenter),
                Period = GetCell(ExpectedColumns.Period),
                AdrN = GetCell(ExpectedColumns.AdrN)
            };

            if (string.IsNullOrWhiteSpace(raw.Ls))
            {
                errors++;
                messages.Add($"Строка {r}: пустое поле ЛС  пропущено.");
                continue;
            }

            // Парсинг чисел/дат
            decimal debtStart = ParseDecimal(raw.DebtStart, ru);
            decimal accrued   = ParseDecimal(raw.Accrued, ru);
            decimal paid      = ParseDecimal(raw.Paid, ru);
            decimal debtEnd   = ParseDecimal(raw.DebtEnd, ru);

            if (!TryParsePeriod(raw.Period, out var periodDate))
            {
                errors++;
                messages.Add($"Строка {r}: не удалось распознать 'Период' = '{raw.Period}'.");
                continue;
            }

            int? monthsInDebt = null;
            if (int.TryParse(raw.MonthsInDebt?.Trim(), out var mid)) monthsInDebt = mid;

            DateOnly? lsClose = null;
            if (TryParseDate(raw.LsCloseDate, out var lcd)) lsClose = lcd;

            // Upsert Account
            var accKey = $"{(raw.AccrualCenter ?? "").Trim()}|{raw.Ls!.Trim()}";
            if (!accCache.TryGetValue(accKey, out var account))
            {
                account = await _db.Accounts
                    .AsTracking()
                    .FirstOrDefaultAsync(a =>
                        a.Ls == raw.Ls!.Trim() &&
                        (a.AccrualCenter ?? "") == (raw.AccrualCenter ?? "").Trim())
                    .ConfigureAwait(false);

                if (account is null)
                {
                    account = new Account
                    {
                        Id = Guid.NewGuid(),
                        Ls = raw.Ls!.Trim(),
                        LsCode = NullIfEmpty(raw.LsCode),
                        Fio = NullIfEmpty(raw.Fio),
                        AddressRaw = NullIfEmpty(raw.Address) ?? "",
                        AddressNormJson = null,
                        PremisesType = NormalizePremises(raw.PremisesType),
                        LsStatus = NullIfEmpty(raw.LsStatus),
                        LsCloseDate = lsClose,
                        LsType = NullIfEmpty(raw.LsType),
                        MgmtStatus = NullIfEmpty(raw.MgmtStatus),
                        Organization = NullIfEmpty(raw.Organization),
                        GroupCompany = NullIfEmpty(raw.GroupCompany),
                        Division = NullIfEmpty(raw.Division),
                        DivisionHead = NullIfEmpty(raw.DivisionHead),
                        AccrualCenter = NullIfEmpty(raw.AccrualCenter),
                        ObjectName = NullIfEmpty(raw.ObjectName),
                        District = NullIfEmpty(raw.District),
                        House = NullIfEmpty(raw.House),
                        AdrN = NullIfEmpty(raw.AdrN),
                        RoomNo = NullIfEmpty(raw.RoomNo)
                    };
                    _db.Accounts.Add(account);
                }
                else
                {
                    account.Fio = Pick(account.Fio, raw.Fio);
                    account.AddressNormJson ??= null;
                    account.PremisesType = NormalizePremises(Pick(account.PremisesType, raw.PremisesType));
                    account.LsStatus = Pick(account.LsStatus, raw.LsStatus);
                    account.LsCloseDate ??= lsClose;
                    account.LsType = Pick(account.LsType, raw.LsType);
                    account.MgmtStatus = Pick(account.MgmtStatus, raw.MgmtStatus);
                    account.Organization = Pick(account.Organization, raw.Organization);
                    account.GroupCompany = Pick(account.GroupCompany, raw.GroupCompany);
                    account.Division = Pick(account.Division, raw.Division);
                    account.DivisionHead = Pick(account.DivisionHead, raw.DivisionHead);
                    account.AccrualCenter = Pick(account.AccrualCenter, raw.AccrualCenter);
                    account.ObjectName = Pick(account.ObjectName, raw.ObjectName);
                    account.District = Pick(account.District, raw.District);
                    account.House = Pick(account.House, raw.House);
                    account.AdrN = Pick(account.AdrN, raw.AdrN);
                    account.RoomNo = Pick(account.RoomNo, raw.RoomNo);
                }

                accCache[accKey] = account;
            }

            // Upsert PeriodBalance
            var existingPb = await _db.PeriodBalances
                .FirstOrDefaultAsync(p => p.AccountId == account.Id && p.PeriodDate == periodDate)
                .ConfigureAwait(false);

            if (existingPb is null)
            {
                var pb = new PeriodBalance
                {
                    Id = Guid.NewGuid(),
                    AccountId = account.Id,
                    PeriodDate = periodDate,
                    DebtStart = debtStart,
                    Accrued = accrued,
                    Paid = paid,
                    DebtEnd = debtEnd,
                    MonthsInDebt = monthsInDebt,
                    DebtCategory = NullIfEmpty(raw.DebtCategory),
                    DebtStructure = NullIfEmpty(raw.DebtStructure),
                    SrcFile = NullIfEmpty(raw.File),
                    RoomNo = NullIfEmpty(raw.RoomNo)
                };
                _db.PeriodBalances.Add(pb);
            }
            else
            {
                existingPb = new PeriodBalance
                {
                    Id = existingPb.Id,
                    AccountId = account.Id,
                    PeriodDate = periodDate,
                    DebtStart = debtStart,
                    Accrued = accrued,
                    Paid = paid,
                    DebtEnd = debtEnd,
                    MonthsInDebt = monthsInDebt,
                    DebtCategory = NullIfEmpty(raw.DebtCategory),
                    DebtStructure = NullIfEmpty(raw.DebtStructure),
                    SrcFile = NullIfEmpty(raw.File),
                    RoomNo = NullIfEmpty(raw.RoomNo)
                };
                _db.Entry(existingPb).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
            }

            // Балансовая проверка (мягкая)
            var balanced = Math.Abs((debtStart + accrued - paid) - debtEnd) <= 0.01m;
            if (!balanced)
                messages.Add($"Строка {r}: несходится баланс (начало+начислено-оплачено != конец).");

            rowsImported++;
        }

        await _db.SaveChangesAsync().ConfigureAwait(false);
        return new ImportReportDto(rowsRead, rowsImported, errors, messages);
    }

    // --- helpers (локальные копии из CsvImporter) ---

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string? Pick(string? current, string? incoming) => string.IsNullOrWhiteSpace(incoming) ? current : incoming!.Trim();

    private static decimal ParseDecimal(string? s, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0m;
        var t = s.Trim().Replace(" ", "");
        if (decimal.TryParse(t, System.Globalization.NumberStyles.Any, culture, out var d)) return d;
        if (decimal.TryParse(t.Replace(",", "."), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d;
        return 0m;
    }

    private static bool TryParseDate(string? s, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim();
        if (DateTime.TryParseExact(t, new[] { "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy" },
                CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt))
        {
            date = new DateOnly(dt.Year, dt.Month, dt.Day);
            return true;
        }
        return false;
    }

    private static bool TryParsePeriod(string? s, out DateOnly period)
    {
        // Период интерпретируем как 1-е число месяца
        period = default;
        if (!TryParseDate(s, out var d)) return false;
        period = new DateOnly(d.Year, d.Month, 1);
        return true;
    }

    private static Dictionary<string, int> BuildColumnMap(IReadOnlyList<string> header)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < header.Count; i++)
        {
            var name = header[i].Trim();
            if (!map.ContainsKey(name))
                map.Add(name, i);
        }
        return map;
    }

    private static string? NormalizePremises(string? raw)
    {
        var s = raw?.Trim();
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (string.Equals(s, "Отдельная квартира", StringComparison.OrdinalIgnoreCase)) return "Квартира";
        return s;
    }
}
