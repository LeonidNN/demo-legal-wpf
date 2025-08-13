using System;
using System.Text.Json;

namespace DemoLegal.Domain.Entities;

/// <summary>
/// Карточка дела на взыскание (основные вычисленные поля, нужные для генерации документов).
/// </summary>
public sealed class CaseFile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AccountId { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public CaseStatus Status { get; set; } = CaseStatus.Candidate;
    public DebtorType DebtorType { get; set; } = DebtorType.Person;

    public decimal DebtAmount { get; set; }           // текущий долг по последнему периоду
    public DateOnly PeriodFrom { get; set; }          // начало периода долга (расчитанное)
    public DateOnly PeriodTo { get; set; }            // конец периода долга (из последнего периода)

    public string ServiceKind { get; set; } = "ЖКУ (обобщ.)";
    public string MgmtStatusText { get; set; } = string.Empty;

    /// <summary>Флаги и недостающие данные, которые нужно добрать (JSON).</summary>
    public string EnrichmentFlagsJson { get; set; } = "{}";

    public EnrichmentFlags GetFlags()
        => JsonSerializer.Deserialize<EnrichmentFlags>(EnrichmentFlagsJson) ?? new EnrichmentFlags();

    public void SetFlags(EnrichmentFlags flags)
        => EnrichmentFlagsJson = JsonSerializer.Serialize(flags);
}

/// <summary>
/// Набор флагов недостающих реквизитов (хранится как JSON в CaseFile).
/// </summary>
public sealed class EnrichmentFlags
{
    public bool NeedInn { get; set; } = false;           // для юрлица
    public bool NeedBirthDate { get; set; } = false;     // для физлица
    public bool NeedBirthPlace { get; set; } = false;    // для физлица
    public bool NeedPeriodRefine { get; set; } = false;  // пересчёт периода после интеграций
}
