using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using DemoLegal.Application.DTOs;

namespace DemoLegal.Application.Abstractions;

/// <summary>Импорт входного файла (xlsx/csv) в БД.</summary>
public interface IImporter
{
    Task<ImportReportDto> ImportAsync(string path);
}

/// <summary>Сервис создания/обновления дела на основании последних данных по ЛС.</summary>
public interface ICaseService
{
    Task<CaseFileDto> UpsertCaseAsync(AccountDto account, PeriodBalanceDto lastPeriod);
}

/// <summary>Генерация документов из шаблонов (досудебка/суд/ФССП).</summary>
public interface IDocumentService
{
    Task<DocPackageResultDto> BuildPretrialAsync(Guid caseId);
    Task<DocPackageResultDto> BuildCourtAsync(Guid caseId);
    Task<DocPackageResultDto> BuildFsspAsync(Guid caseId);
}
