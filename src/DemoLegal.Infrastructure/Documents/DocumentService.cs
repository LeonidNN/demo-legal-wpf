using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using DemoLegal.Application.Abstractions;
using DemoLegal.Application.DTOs;
using DemoLegal.Domain;
using DemoLegal.Infrastructure.Files;
using DemoLegal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DemoLegal.Infrastructure.Documents;

/// <summary>
/// Реализация IDocumentService (MVP):
/// - собирает данные по делу из БД,
/// - подставляет в текстовые шаблоны (Resources/Templates),
/// - сохраняет готовые файлы в папке дела.
/// Позже можно заменить шаблоны на DOCX→PDF.
/// </summary>
public sealed class DocumentService : IDocumentService
{
    private readonly DemoContext _db;

    public DocumentService(DemoContext db) => _db = db;

    public async Task<DocPackageResultDto> BuildPretrialAsync(Guid caseId)
    {
        var (ok, data, warnings) = await LoadCaseDataAsync(caseId).ConfigureAwait(false);
        if (!ok) return new DocPackageResultDto(caseId, false, "", Array.Empty<string>(), warnings);

        var outDir = PathService.GetCaseFolder(caseId);
        var files = new List<string>();
        var vars = VarsFrom(data);

        // 1) Претензия (template: Pretrial_Claim.txt)
        files.Add(await RenderToFileAsync("Pretrial_Claim.txt", vars, Path.Combine(outDir, "Претензия.txt")).ConfigureAwait(false));

        // 2) Расчёт задолженности (template: Debt_Calc.txt)
        files.Add(await RenderToFileAsync("Debt_Calc.txt", vars, Path.Combine(outDir, "Расчет_задолженности.txt")).ConfigureAwait(false));

        // 3) Реестр отправок (template: Dispatch_Register.txt)
        files.Add(await RenderToFileAsync("Dispatch_Register.txt", vars, Path.Combine(outDir, "Реестр_отправок.txt")).ConfigureAwait(false));

        return new DocPackageResultDto(caseId, true, outDir, files, warnings);
    }

    public Task<DocPackageResultDto> BuildCourtAsync(Guid caseId)
        => Task.FromResult(new DocPackageResultDto(caseId, false, "", Array.Empty<string>(), new[] { "Пока не реализовано" }));

    public Task<DocPackageResultDto> BuildFsspAsync(Guid caseId)
        => Task.FromResult(new DocPackageResultDto(caseId, false, "", Array.Empty<string>(), new[] { "Пока не реализовано" }));

    // ---------- helpers ----------

    private async Task<(bool ok, CaseDocData data, string[] warnings)> LoadCaseDataAsync(Guid caseId)
    {
        var culture = new CultureInfo("ru-RU");

        var cf = await _db.CaseFiles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == caseId).ConfigureAwait(false);
        if (cf is null)
            return (false, new CaseDocData(), new[] { $"Дело не найдено: {caseId}" });

        var acc = await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == cf.AccountId).ConfigureAwait(false);
        if (acc is null)
            return (false, new CaseDocData(), new[] { $"ЛС для дела не найден: {cf.AccountId}" });

        // имя должника
        var debtorName = cf.DebtorType == DebtorType.Person
            ? (acc.Fio ?? "Неизвестный должник")
            : (acc.Organization ?? "Застройщик (наименование не указано)");

        var data = new CaseDocData
        {
            CaseId = cf.Id,
            DebtorKind = cf.DebtorType == DemoLegal.Domain.DebtorType.Person ? "Физическое лицо" : "Юридическое лицо",
            DebtorName = debtorName,
            Address = acc.DisplayAddress(),
            Ls = acc.Ls,
            DebtAmount = cf.DebtAmount,
            PeriodFrom = $"{cf.PeriodFrom:MM.yyyy}",
            PeriodTo = $"{cf.PeriodTo:MM.yyyy}",
            ServiceKind = cf.ServiceKind,
            MgmtStatusText = cf.MgmtStatusText,
            Organization = acc.Organization ?? "УК"
        };

        var warnings = new List<string>();
        if (cf.DebtorType == DebtorType.Person && string.IsNullOrWhiteSpace(acc.Fio))
            warnings.Add("ФИО должника отсутствует.");
        if (cf.DebtorType == DebtorType.Company && string.IsNullOrWhiteSpace(acc.Organization))
            warnings.Add("Наименование юрлица отсутствует.");

        return (true, data, warnings.ToArray());
    }

    private static Dictionary<string, string> VarsFrom(CaseDocData d) => new()
    {
        ["CaseId"] = d.CaseId.ToString(),
        ["DebtorKind"] = d.DebtorKind,
        ["DebtorName"] = d.DebtorName,
        ["Address"] = d.Address,
        ["Ls"] = d.Ls,
        ["DebtAmount"] = d.DebtAmount.ToString("N2", new CultureInfo("ru-RU")),
        ["PeriodFrom"] = d.PeriodFrom,
        ["PeriodTo"] = d.PeriodTo,
        ["ServiceKind"] = d.ServiceKind,
        ["MgmtStatusText"] = d.MgmtStatusText,
        ["Organization"] = d.Organization
    };

    private static async Task<string> RenderToFileAsync(string templateName, Dictionary<string, string> vars, string outPath)
    {
        var exeDir = AppContext.BaseDirectory;
        var templatePath = Path.Combine(exeDir, "Resources", "Templates", templateName);
        var template = TemplateEngine.ReadTemplate(templatePath);
        if (string.IsNullOrEmpty(template))
        {
            // если шаблон отсутствует, сформируем простой текст по умолчанию
            template = DefaultTemplateFor(templateName);
        }
        var filled = TemplateEngine.Render(template, vars);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        await File.WriteAllTextAsync(outPath, filled).ConfigureAwait(false);
        return outPath;
    }

    private static string DefaultTemplateFor(string name) => name switch
    {
        "Pretrial_Claim.txt" =>
            "Претензия\nДело: {{CaseId}}\nДолжник: {{DebtorKind}} {{DebtorName}}\nЛС: {{Ls}}\nАдрес: {{Address}}\nПериод: {{PeriodFrom}}–{{PeriodTo}}\nСумма долга: {{DebtAmount}}\nУслуги: {{ServiceKind}}\n{{MgmtStatusText}}\nВзыскатель: {{Organization}}\n",
        "Debt_Calc.txt" =>
            "Расчет задолженности\nЛС: {{Ls}}\nПериод: {{PeriodFrom}}–{{PeriodTo}}\nСумма долга на период: {{DebtAmount}}\n(Подробная таблица начислений/оплат будет добавлена на шаге пени/детализаций)\n",
        "Dispatch_Register.txt" =>
            "Реестр отправок\nДело: {{CaseId}}\nПолучатель: {{DebtorName}}, адрес: {{Address}}\nСопроводительная документация: Претензия, Расчет задолженности\n",
        _ => "Документ\nДело: {{CaseId}}\n"
    };
}
