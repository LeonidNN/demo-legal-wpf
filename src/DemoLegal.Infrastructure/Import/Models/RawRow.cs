using System;

namespace DemoLegal.Infrastructure.Import.Models;

/// <summary>
/// Сырые данные одной строки импорта (после парсинга CSV).
/// </summary>
public sealed class RawRow
{
    public string? File { get; init; }
    public string? RoomNo { get; init; }
    public string? GroupCompany { get; init; }
    public string? Organization { get; init; }
    public string? House { get; init; }
    public string? Address { get; init; }
    public string? Ls { get; init; }
    public string? LsCode { get; init; }
    public string? Fio { get; init; }
    public string? DebtStart { get; init; }
    public string? Accrued { get; init; }
    public string? Paid { get; init; }
    public string? DebtEnd { get; init; }
    public string? DebtStructure { get; init; }
    public string? MonthsInDebt { get; init; }
    public string? DebtCategory { get; init; }
    public string? MgmtStatus { get; init; }
    public string? District { get; init; }
    public string? ObjectName { get; init; }
    public string? Division { get; init; }
    public string? DivisionHead { get; init; }
    public string? PremisesType { get; init; }
    public string? LsStatus { get; init; }
    public string? LsCloseDate { get; init; }
    public string? LsType { get; init; }
    public string? AccrualCenter { get; init; }
    public string? Period { get; init; }
    public string? AdrN { get; init; }
}
