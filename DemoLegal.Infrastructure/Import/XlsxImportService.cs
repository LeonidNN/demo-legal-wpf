using System.Data;
using System.Globalization;
using System.Text.Json;
using DemoLegal.Domain.Models;
using DemoLegal.Infrastructure.Database;
using DemoLegal.Infrastructure.Repositories;
using ExcelDataReader;

namespace DemoLegal.Infrastructure.Import;

public sealed class XlsxImportService
{
    private readonly IAccountRepository _accounts = new AccountRepository();
    private readonly IPeriodBalanceRepository _balances = new PeriodBalanceRepository();
    private readonly ICaseFileRepository _cases = new CaseFileRepository();

    // ключевые колонки, по которым распознаём "ту самую" строку заголовков
    private static readonly string[] HeaderMustHave = new[]
    {
        "ЛС", "Адрес", "Период", "Задолженность на конец"
    };

    public async Task<ImportSummary> ImportXlsxAsync(string path, int? rowLimit = null)
    {
        SqliteConnectionFactory.Configure();
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        var summary = new ImportSummary();

        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        // Читаем весь лист вручную, без UseHeaderRow
        var rows = new List<object?[]>();
        do
        {
            while (reader.Read())
            {
                var arr = new object?[reader.FieldCount];
                for (int i = 0; i < reader.FieldCount; i++) arr[i] = reader.GetValue(i);
                rows.Add(arr);
            }
        } while (reader.NextResult()); // берём только первый лист — как и раньше

        if (rows.Count == 0) return summary;

        string Norm(string s) => (s ?? string.Empty).Replace('\u00A0',' ').Trim();

        // Ищем строку заголовков в первых 200 строках
        int headerIndex = -1;
        string[] headerRaw = Array.Empty<string>();
        for (int r = 0; r < Math.Min(rows.Count, 200); r++)
        {
            var raw = rows[r].Select(v => v?.ToString() ?? string.Empty).ToArray();
            var norm = raw.Select(Norm).ToArray();

            // считаем строку заголовком, если все обязательные поля присутствуют (без учёта регистра)
            bool ok = HeaderMustHave.All(h => norm.Any(c => c.Equals(h, StringComparison.OrdinalIgnoreCase) || c.Contains(h, StringComparison.OrdinalIgnoreCase)));
            if (ok)
            {
                headerIndex = r;
                headerRaw = raw;
                break;
            }
        }

        if (headerIndex < 0)
        {
            summary.Warnings.Add("Не удалось найти строку заголовков (не нашли колонки ЛС/Адрес/Период/Задолженность на конец в первых 200 строках).");
            return summary;
        }

        // Карта «нормализованное имя -> индекс колонки»
        var headerNorm = headerRaw.Select(Norm).ToArray();
        int FindColIndex(params string[] candidates)
        {
            foreach (var cand in candidates)
            {
                for (int i = 0; i < headerNorm.Length; i++)
                {
                    if (headerNorm[i].Equals(cand, StringComparison.OrdinalIgnoreCase)) return i;
                }
            }
            return -1;
        }

        int idxLs           = FindColIndex("ЛС", "Лицевой счёт");
        int idxOrg          = FindColIndex("Организация");
        int idxAddr         = FindColIndex("Адрес");
        int idxPeriod       = FindColIndex("Период");
        int idxDebtStart    = FindColIndex("Задолженность на начало");
        int idxAccrued      = FindColIndex("Начислено");
        int idxPaid         = FindColIndex("Оплачено");
        int idxDebtEnd      = FindColIndex("Задолженность на конец");
        int idxMonthsInDebt = FindColIndex("Месяцы задолженности"," Месяцы задолженности");
        int idxDebtStruct   = FindColIndex("Структура долга");

        string Cell(object?[] row, int idx)
            => (idx >= 0 && idx < row.Length && row[idx] is not null && row[idx] != DBNull.Value)
                ? row[idx]!.ToString() ?? string.Empty
                : string.Empty;

        decimal Money(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0m;
            raw = raw.Replace('\u00A0',' ').Replace(" ", "");
            raw = raw.Replace(",", ".");
            return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
        }

        DateOnly ParsePeriod(string raw)
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

        DateTime? ParseNullableDate(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (DateTime.TryParse(raw, new CultureInfo("ru-RU"), DateTimeStyles.None, out var dt))
                return dt.Date;
            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var oa) && oa > 20000 && oa < 80000)
                return DateTime.FromOADate(oa).Date;
            return null;
        }

        int? ParseNullableInt(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            raw = raw.Replace('\u00A0',' ').Trim();
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
        }

        // Основной проход: начиная со следующей строки после заголовка
        int processed = 0;
        for (int r = headerIndex + 1; r < rows.Count; r++) { if (rowLimit.HasValue && summary.RowsRead >= rowLimit.Value) break;
            var row = rows[r];
            summary.RowsRead++;

            var ls = Norm(Cell(row, idxLs));
            if (string.IsNullOrWhiteSpace(ls))
            {
                summary.Warnings.Add($"Строка {summary.RowsRead}: пустой ЛС — пропуск.");
                continue;
            }

            var period   = ParsePeriod(Cell(row, idxPeriod));
            var org      = Norm(Cell(row, idxOrg));
            var address  = Norm(Cell(row, idxAddr));

            var debtStart = Money(Cell(row, idxDebtStart));
            var accrued   = Money(Cell(row, idxAccrued));
            var paid      = Money(Cell(row, idxPaid));
            var debtEnd   = Money(Cell(row, idxDebtEnd));

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
                LsCode = Cell(row, FindColIndex("Код ЛС")),
                Fio = Cell(row, FindColIndex("ФИО")),
                AddressRaw = address,
                AddressNorm = null,
                PremisesType = Cell(row, FindColIndex("Тип помещения")),
                LsStatus = Cell(row, FindColIndex("Статус ЛС")),
                LsCloseDate = ParseNullableDate(Cell(row, FindColIndex("Дата закрытия ЛС"))),
                LsType = Cell(row, FindColIndex("Тип ЛС")),
                MgmtStatus = Cell(row, FindColIndex("Статус управления домом")),
                Organization = string.IsNullOrWhiteSpace(org) ? "Организация" : org,
                GroupCompany = Cell(row, FindColIndex("ГК")),
                Division = Cell(row, FindColIndex("Дивизионы")),
                DivisionHead = Cell(row, FindColIndex("Руководитель дивизиона")),
                AccrualCenter = Cell(row, FindColIndex("Центр начислений")),
                ObjectName = Cell(row, FindColIndex("Объект")),
                District = Cell(row, FindColIndex("Район")),
                House = Cell(row, FindColIndex("Дом")),
                AdrN = Cell(row, FindColIndex("АдрН"))
            };

            var accountId = await _accounts.GetOrCreateAsync(acc);

            var monthsInDebt = ParseNullableInt(Cell(row, idxMonthsInDebt));
            var pb = new PeriodBalance
            {
                AccountId = accountId,
                PeriodDate = period,
                DebtStart = debtStart,
                Accrued = accrued,
                Paid = paid,
                DebtEnd = debtEnd,
                MonthsInDebt = monthsInDebt,
                DebtCategory = Cell(row, FindColIndex("Категория долга")),
                DebtStructure = Cell(row, idxDebtStruct),
                SrcFile = Cell(row, FindColIndex("Файл")),
                RoomNo = Cell(row, FindColIndex("№скв"))
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
            processed++;
        }

        // Диагностика для отчёта
        summary.Warnings.Insert(0, $"Инфо: строка заголовков обнаружена на строке {headerIndex + 1} листа (считая от 1). Обработано строк данных: {processed}.");

        return summary;
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

