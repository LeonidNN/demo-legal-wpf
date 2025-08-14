using System.Globalization;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using DemoLegal.Domain.Models;
using DemoLegal.Infrastructure.Repositories;
using DemoLegal.Infrastructure.Database;
using System.Text.Json;

namespace DemoLegal.Infrastructure.Import;

public sealed class CsvImportService
{
    private readonly IAccountRepository _accounts = new AccountRepository();
    private readonly IPeriodBalanceRepository _balances = new PeriodBalanceRepository();
    private readonly ICaseFileRepository _cases = new CaseFileRepository();

    public async Task<ImportSummary> ImportCsvAsync(string path, int? rowLimit = null)
    {
        SqliteConnectionFactory.Configure(); // ensure DB
        var summary = new ImportSummary();

        var delimiter = DetectDelimiter(path); // ";" или "\t"

        var cfg = new CsvConfiguration(new CultureInfo("ru-RU"))
        {
            Delimiter = delimiter,
            HasHeaderRecord = true,
            BadDataFound = null,
            MissingFieldFound = null,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim
        };

        using var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, cfg);

        if (!await csv.ReadAsync())
            return summary;

        csv.ReadHeader();

        // --- безопасная работа с заголовками ---
        string Norm(string s) => (s ?? string.Empty).Replace('\u00A0',' ').Trim();

        var originalHeader = csv.HeaderRecord ?? Array.Empty<string>();
        var headerNorm = originalHeader.Select(Norm).ToArray();

        string? FindCol(string target)
        {
            target = Norm(target);
            for (int i = 0; i < headerNorm.Length; i++)
                if (headerNorm[i].Equals(target, StringComparison.OrdinalIgnoreCase))
                    return originalHeader[i]; // вернуть «как в файле»
            return null;
        }

        var colLs          = FindCol("ЛС") ?? FindCol("Лицевой счёт");
        var colOrg         = FindCol("Организация");
        var colAddr        = FindCol("Адрес");
        var colPeriod      = FindCol("Период");
        var colDebtStart   = FindCol("Задолженность на начало");
        var colAccrued     = FindCol("Начислено");
        var colPaid        = FindCol("Оплачено");
        var colDebtEnd     = FindCol("Задолженность на конец");
        var colMonthsInDebt= FindCol("Месяцы задолженности") ?? FindCol(" Месяцы задолженности");
        var colDebtStruct  = FindCol("Структура долга");

        while (await csv.ReadAsync()) { if (rowLimit.HasValue && summary.RowsRead >= rowLimit.Value) break;
            summary.RowsRead++;

            string GF(string? name)  => string.IsNullOrWhiteSpace(name) ? string.Empty : (csv.GetField(name) ?? string.Empty);
            string? GFO(string? name)=> string.IsNullOrWhiteSpace(name) ? null         : (csv.GetField(name) ?? null);

            var ls = Norm(GF(colLs));
            if (string.IsNullOrWhiteSpace(ls))
            {
                summary.Warnings.Add($"Строка {summary.RowsRead}: пустой ЛС — пропуск.");
                continue;
            }

            var org = Norm(GF(colOrg));
            var address   = Norm(GF(colAddr));
            var periodStr = GF(colPeriod);
            var period    = ParsePeriod(periodStr);

            decimal ParseMoney(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return 0m;
                raw = raw.Replace('\u00A0',' ').Replace(" ", "");
                raw = raw.Replace(",", ".");
                return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
            }

            var debtStart = ParseMoney(GF(colDebtStart));
            var accrued   = ParseMoney(GF(colAccrued));
            var paid      = ParseMoney(GF(colPaid));
            var debtEnd   = ParseMoney(GF(colDebtEnd));

            var diff = Math.Abs((double)((debtStart + accrued - paid) - debtEnd));
            if (diff > 0.01)
            {
                summary.BalanceMismatches++;
                summary.Warnings.Add($"ЛС {ls} {period:yyyy-MM}: баланс не сходится (+/- {diff:0.00}). Строка пропущена.");
                continue;
            }

            var acc = new Account
            {
                Ls = ls,
                LsCode = GFO(FindCol("Код ЛС")),
                Fio = GFO(FindCol("ФИО")),
                AddressRaw = address,
                AddressNorm = null,
                PremisesType = GFO(FindCol("Тип помещения")),
                LsStatus = GFO(FindCol("Статус ЛС")),
                LsCloseDate = ParseNullableDate(GFO(FindCol("Дата закрытия ЛС"))),
                LsType = GFO(FindCol("Тип ЛС")),
                MgmtStatus = GFO(FindCol("Статус управления домом")),
                Organization = string.IsNullOrWhiteSpace(org) ? "Организация" : org,
                GroupCompany = GFO(FindCol("ГК")),
                Division = GFO(FindCol("Дивизионы")),
                DivisionHead = GFO(FindCol("Руководитель дивизиона")),
                AccrualCenter = GFO(FindCol("Центр начислений")),
                ObjectName = GFO(FindCol("Объект")),
                District = GFO(FindCol("Район")),
                House = GFO(FindCol("Дом")),
                AdrN = GFO(FindCol("АдрН"))
            };

            var accountId = await _accounts.GetOrCreateAsync(acc);

            var monthsInDebt = ParseNullableInt(GFO(colMonthsInDebt));
            var pb = new PeriodBalance
            {
                AccountId = accountId,
                PeriodDate = period,
                DebtStart = debtStart,
                Accrued = accrued,
                Paid = paid,
                DebtEnd = debtEnd,
                MonthsInDebt = monthsInDebt,
                DebtCategory = GFO(FindCol("Категория долга")),
                DebtStructure = GFO(colDebtStruct),
                SrcFile = GFO(FindCol("Файл")),
                RoomNo = GFO(FindCol("№скв"))
            };
            await _balances.UpsertAsync(pb);

            var latest = await _balances.GetLatestByAccountAsync(accountId);
            if (latest is not null)
            {
                var (periodFrom, periodTo) = CalcPeriodRange(latest.PeriodDate, latest.MonthsInDebt);
                var debtorType = (acc.LsType ?? "").Trim().Equals("Распределенные", StringComparison.OrdinalIgnoreCase)
                                 ? "person" : "company";
                var mgmtText = BuildMgmtStatusText(acc, periodTo);

                var flags = new Dictionary<string,bool>();
                if (debtorType == "person") { flags["need_birth_date"] = true; flags["need_birth_place"] = true; }
                else { flags["need_inn"] = true; }

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

    private static string DetectDelimiter(string path)
    {
        using var fs = File.OpenRead(path);
        using var sr = new StreamReader(fs, detectEncodingFromByteOrderMarks: true);
        var sample = sr.ReadLine() ?? "";
        int sc = sample.Count(c => c == ';');
        int tc = sample.Count(c => c == '\t');
        return tc > sc ? "\t" : ";";
    }

    private static DateOnly ParsePeriod(string raw)
    {
        raw = (raw ?? string.Empty).Trim();
        if (DateTime.TryParse(raw, new CultureInfo("ru-RU"), DateTimeStyles.None, out var dt))
            return new DateOnly(dt.Year, dt.Month, 1);
        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var oa) && oa > 20000 && oa < 80000)
        {
            var d = DateTime.FromOADate(oa);
            return new DateOnly(d.Year, d.Month, 1);
        }
        var now = DateTime.Now;
        return new DateOnly(now.Year, now.Month, 1);
    }

    private static DateTime? ParseNullableDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (DateTime.TryParse(raw, new CultureInfo("ru-RU"), DateTimeStyles.None, out var dt))
            return dt.Date;
        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var oa) && oa > 20000 && oa < 80000)
            return DateTime.FromOADate(oa).Date;
        return null;
    }

    private static int? ParseNullableInt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Replace('\u00A0',' ').Trim();
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
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

