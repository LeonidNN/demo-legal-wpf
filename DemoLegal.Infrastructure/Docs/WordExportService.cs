using System.Globalization;
using System.Text;
using Dapper;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DemoLegal.Infrastructure.Database;

namespace DemoLegal.Infrastructure.Docs;

public sealed class WordExportService
{
    public sealed record ExportRow(
        int AccountId,
        string? Fio,
        string Address,
        string Ls,
        string? LsCode,
        string Organization,
        string DebtorType,                 // person|company
        decimal DebtAmount,
        DateOnly PeriodFrom,
        DateOnly PeriodTo,
        string MgmtStatusText
    );

    /// <summary>
    /// Загружает все нужные поля из БД по account_ids
    /// </summary>
    public async Task<List<ExportRow>> LoadAsync(IEnumerable<int> accountIds)
    {
        using var db = SqliteConnectionFactory.Create();
        var sql = @"
select
  a.id as AccountId,
  a.fio as Fio,
  coalesce(a.address_norm, a.address_raw) as Address,
  a.ls as Ls,
  a.ls_code as LsCode,
  a.organization as Organization,
  cf.debtor_type as DebtorType,
  cf.debt_amount as DebtAmount,
  cf.period_from as PeriodFrom,
  cf.period_to as PeriodTo,
  cf.mgmt_status_text as MgmtStatusText
from account a
join case_file cf on cf.account_id = a.id
where a.id in @ids
";
        var rows = (await db.QueryAsync(sql, new { ids = accountIds.ToArray() }))
            .Select(r =>
            {
                // SQLite хранит даты как текст YYYY-MM-01 — распарсим
                DateOnly pFrom = ParseDateOnly((string)r.PeriodFrom);
                DateOnly pTo   = ParseDateOnly((string)r.PeriodTo);
                decimal debt   = Convert.ToDecimal(r.DebtAmount, CultureInfo.InvariantCulture);
                return new ExportRow(
                    AccountId: (int)r.AccountId,
                    Fio: (string?)r.Fio,
                    Address: (string)r.Address,
                    Ls: (string)r.Ls,
                    LsCode: (string?)r.LsCode,
                    Organization: (string)r.Organization,
                    DebtorType: ((string)r.DebtorType) ?? "person",
                    DebtAmount: debt,
                    PeriodFrom: pFrom,
                    PeriodTo: pTo,
                    MgmtStatusText: (string)r.MgmtStatusText
                );
            })
            .ToList();

        return rows;

        static DateOnly ParseDateOnly(string s)
        {
            // ожидаем yyyy-MM-01
            if (DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return new DateOnly(dt.Year, dt.Month, dt.Day);
            if (DateTime.TryParse(s, out var dt2))
                return new DateOnly(dt2.Year, dt2.Month, dt2.Day);
            var now = DateTime.Now;
            return new DateOnly(now.Year, now.Month, 1);
        }
    }

    /// <summary>
    /// Генерирует по документу на каждый AccountId в указанную папку
    /// </summary>
    public async Task<int> ExportAsync(IEnumerable<int> accountIds, string targetFolder)
    {
        var list = await LoadAsync(accountIds);
        int counter = 0;
        Directory.CreateDirectory(targetFolder);

        foreach (var r in list)
        {
            var fileName = Path.Combine(targetFolder,
                SafeName($"{r.AccountId}-{(r.Fio ?? r.Ls)}-{r.PeriodFrom:yyyyMM}-{r.PeriodTo:yyyyMM}.docx"));

            using var doc = WordprocessingDocument.Create(fileName, WordprocessingDocumentType.Document);
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());

            var body = mainPart.Document.Body!;
            // Заголовок
            body.Append(MkPara($"Пакет документов по взысканию задолженности", bold: true, size: 30));
            body.Append(MkPara($"УК: {r.Organization}"));
            body.Append(MkPara($"Должник: {(r.DebtorType == "person" ? (r.Fio ?? "(ФИО не указано)") : "(ЮЛ)")}"));
            body.Append(MkPara($"Лицевой счёт: {r.Ls} {(string.IsNullOrWhiteSpace(r.LsCode) ? "" : $"({r.LsCode})")}"));
            body.Append(MkPara($"Адрес: {r.Address}"));
            body.Append(MkPara($"Период долга: {r.PeriodFrom:MM.yyyy} — {r.PeriodTo:MM.yyyy}"));
            body.Append(MkPara($"Сумма долга: {r.DebtAmount:N2} руб."));
            body.Append(MkPara($"{r.MgmtStatusText}"));
            body.Append(new Paragraph(new Run(new Break())));
            body.Append(MkPara("Состав комплекта (черновик):"));
            body.Append(MkList(new []
            {
                "Претензия (досудебная)",
                "Заявление о выдаче судебного приказа (или исковое заявление)",
                "Расчёт задолженности",
                "Квитанции/выписки по оплатам (при наличии)"
            }));

            mainPart.Document.Save();
            counter++;
        }

        return counter;
    }

    private static string SafeName(string s)
    {
        var bad = Path.GetInvalidFileNameChars();
        var ok = new string(s.Select(ch => bad.Contains(ch) ? '_' : ch).ToArray());
        return ok;
    }

    private static Paragraph MkPara(string text, bool bold = false, int size = 24)
    {
        var runProps = new RunProperties();
        if (bold) runProps.Append(new Bold());
        runProps.Append(new FontSize { Val = (size*2).ToString() });
        var run = new Run(runProps, new Text(text));
        return new Paragraph(run);
    }

    private static Paragraph MkList(IEnumerable<string> items)
    {
        var p = new Paragraph();
        foreach (var item in items)
        {
            p.Append(new Run(new Text("• " + item)));
            p.Append(new Run(new Break()));
        }
        return p;
    }
}
