using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DemoLegal.Application.Abstractions;
using DemoLegal.Application.DTOs;
using DemoLegal.Domain.Entities;
using DemoLegal.Infrastructure.Persistence;
using DemoLegal.Infrastructure.Import.Models;
using Microsoft.EntityFrameworkCore;

namespace DemoLegal.Infrastructure.Import;

/// <summary>
/// Импорт CSV (UTF-8; разделитель ';'; десятичная ',') без внешних пакетов.
/// Поддержка XSLX будет добавлена отдельным импортером (ClosedXML).
/// </summary>
public sealed class CsvImporter : IImporter
{
    private readonly DemoContext _db;

    public CsvImporter(DemoContext db) => _db = db;

    public async Task<ImportReportDto> ImportAsync(string path)
    {
        if (!File.Exists(path))
            return new ImportReportDto(0, 0, 1, new[] { $"Файл не найден: {path}" });

        var lines = await File.ReadAllLinesAsync(path).ConfigureAwait(false);
        if (lines.Length == 0)
            return new ImportReportDto(0, 0, 1, new[] { "Файл пуст." });

        var header = ParseCsvLine(lines[0]);
        var map = BuildColumnMap(header);

        var ru = new CultureInfo("ru-RU");
        int rowsRead = 0, rowsImported = 0, errors = 0;
        var messages = new List<string>();

        // кэш аккаунтов по ключам (AccrualCenter+Ls) и (Ls)
        var accCache = new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase);

        for (int i = 1; i < lines.Length; i++)
        {
            rowsRead++;
            var values = ParseCsvLine(lines[i]);
            if (values.Count != header.Count)
            {
                errors++;
                messages.Add($"Строка {i+1}: количество колонок не совпадает с заголовком.");
                continue;
            }

            var raw = RowFrom(values, map);

            if (string.IsNullOrWhiteSpace(raw.Ls))
            {
                errors++;
                messages.Add($"Строка {i+1}: пустое поле ЛС — пропущено.");
                continue;
            }

            // Парсинг чисел/дат
            decimal debtStart = ParseDecimal(raw.DebtStart, ru);
            decimal accrued   = ParseDecimal(raw.Accrued, ru);
            decimal paid      = ParseDecimal(raw.Paid, ru);
            decimal debtEnd   = ParseDecimal(raw.DebtEnd, ru);

            DateOnly periodDate;
            if (!TryParseDate(raw.Period, out periodDate))
            {
                errors++;
                messages.Add($"Строка {i+1}: не удалось распознать 'Период' = '{raw.Period}'.");
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
                    // мягкое обновление
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

            // Upsert PeriodBalance (уникальность {AccountId, PeriodDate})
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
                _db.Entry(existingPb).State = EntityState.Modified;
            }

            // Балансовая проверка (мягкая)
            var balanced = Math.Abs((debtStart + accrued - paid) - debtEnd) <= 0.01m;
            if (!balanced)
                messages.Add($"Строка {i+1}: несходится баланс (начало+начислено-оплачено != конец).");

            rowsImported++;
        }

        await _db.SaveChangesAsync().ConfigureAwait(false);
        return new ImportReportDto(rowsRead, rowsImported, errors, messages);
    }

    // ------- helpers -------

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string? Pick(string? current, string? incoming) => string.IsNullOrWhiteSpace(incoming) ? current : incoming!.Trim();

    private static decimal ParseDecimal(string? s, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0m;
        var t = s.Trim().Replace(" ", "");
        // заменим запятую на культуру ru-RU, поддержим точки
        if (decimal.TryParse(t, NumberStyles.Any, culture, out var d)) return d;
        if (decimal.TryParse(t.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d;
        return 0m;
    }

    private static bool TryParseDate(string? s, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim();
        // ожидаем ДД.ММ.ГГГГ; берём как есть
        if (DateTime.TryParseExact(t, new[] { "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            // если это "Период" → считаем первым днём месяца
            return DateOnly.TryParseExact($"01.{dt:MM.yyyy}", "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
        return false;
    }

    private static Dictionary<string, int> BuildColumnMap(IReadOnlyList<string> header)
    {
        var map = new Dictionary<string, int>();
        for (int i = 0; i < header.Count; i++)
        {
            var name = header[i].Trim();
            if (!map.ContainsKey(name))
                map.Add(name, i);
        }
        return map;
    }

    private static RawRow RowFrom(IReadOnlyList<string> values, Dictionary<string, int> map)
    {
        string? Get(string col) => map.TryGetValue(col, out var idx) ? values[idx] : null;

        return new RawRow
        {
            File = Get(ExpectedColumns.File),
            RoomNo = Get(ExpectedColumns.RoomNo),
            GroupCompany = Get(ExpectedColumns.GroupCompany),
            Organization = Get(ExpectedColumns.Organization),
            House = Get(ExpectedColumns.House),
            Address = Get(ExpectedColumns.Address),
            Ls = Get(ExpectedColumns.Ls),
            LsCode = Get(ExpectedColumns.LsCode),
            Fio = Get(ExpectedColumns.Fio),
            DebtStart = Get(ExpectedColumns.DebtStart),
            Accrued = Get(ExpectedColumns.Accrued),
            Paid = Get(ExpectedColumns.Paid),
            DebtEnd = Get(ExpectedColumns.DebtEnd),
            DebtStructure = Get(ExpectedColumns.DebtStructure),
            MonthsInDebt = Get(ExpectedColumns.MonthsInDebt),
            DebtCategory = Get(ExpectedColumns.DebtCategory),
            MgmtStatus = Get(ExpectedColumns.MgmtStatus),
            District = Get(ExpectedColumns.District),
            ObjectName = Get(ExpectedColumns.ObjectName),
            Division = Get(ExpectedColumns.Division),
            DivisionHead = Get(ExpectedColumns.DivisionHead),
            PremisesType = Get(ExpectedColumns.PremisesType),
            LsStatus = Get(ExpectedColumns.LsStatus),
            LsCloseDate = Get(ExpectedColumns.LsCloseDate),
            LsType = Get(ExpectedColumns.LsType),
            AccrualCenter = Get(ExpectedColumns.AccrualCenter),
            Period = Get(ExpectedColumns.Period),
            AdrN = Get(ExpectedColumns.AdrN)
        };
    }

    /// <summary>
    /// Парсер строки CSV с разделителем ';' и поддержкой кавычек ("...").
    /// </summary>
    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        if (line is null) return result;

        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"'); // экранированная кавычка
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ';' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }

    private static string? NormalizePremises(string? raw)
    {
        var s = raw?.Trim();
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (string.Equals(s, "Отдельная квартира", StringComparison.OrdinalIgnoreCase)) return "Квартира";
        return s;
    }
}
