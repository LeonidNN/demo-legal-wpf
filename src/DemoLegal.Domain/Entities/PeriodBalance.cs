using System;

namespace DemoLegal.Domain.Entities;

/// <summary>
/// Баланс по периоду (месяцу) для конкретного ЛС — как в строке входного файла.
/// </summary>
public sealed class PeriodBalance
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AccountId { get; init; }

    /// <summary>Первый день месяца периода (например, 2025-05-01).</summary>
    public DateOnly PeriodDate { get; init; }

    public decimal DebtStart { get; init; }   // "Задолженность на начало"
    public decimal Accrued { get; init; }     // "Начислено"
    public decimal Paid { get; init; }        // "Оплачено" (0, если пусто)
    public decimal DebtEnd { get; init; }     // "Задолженность на конец"

    public int? MonthsInDebt { get; init; }   // "Месяцы задолженности"
    public string? DebtCategory { get; init; }
    public string? DebtStructure { get; init; }
    public string? SrcFile { get; init; }
    public string? RoomNo { get; init; }

    /// <summary>
    /// Проверка бухгалтерского тождества с допуском на округления.
    /// </summary>
    public bool IsBalanced(decimal tolerance = 0.01m)
        => Math.Abs((DebtStart + Accrued - Paid) - DebtEnd) <= tolerance;
}
