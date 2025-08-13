using System;

namespace DemoLegal.Infrastructure.Documents;

/// <summary>
/// Минимальные модели данных для шаблонов документов (можно расширять при интеграциях).
/// </summary>
public sealed class CaseDocData
{
    public Guid CaseId { get; init; }
    public string DebtorKind { get; init; } = "";           // "Физическое лицо" / "Юридическое лицо"
    public string DebtorName { get; init; } = "";           // ФИО или Наименование
    public string Address { get; init; } = "";
    public string Ls { get; init; } = "";
    public decimal DebtAmount { get; init; }
    public string PeriodFrom { get; init; } = "";           // "MM.yyyy"
    public string PeriodTo { get; init; } = "";
    public string ServiceKind { get; init; } = "";
    public string MgmtStatusText { get; init; } = "";
    public string Organization { get; init; } = "";         // взыскатель (кратко)
}
