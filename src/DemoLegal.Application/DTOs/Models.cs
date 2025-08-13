using System;
using System.Collections.Generic;
using DemoLegal.Domain;
using DemoLegal.Domain.Entities;

namespace DemoLegal.Application.DTOs;

/// <summary>Плоские модели для обмена между слоями Application/Infrastructure/UI.</summary>
public sealed record AccountDto(
    Guid Id,
    string Ls,
    string? LsCode,
    string? Fio,
    string AddressRaw,
    string? AddressNormJson,
    string? PremisesType,
    string? LsStatus,
    DateOnly? LsCloseDate,
    string? LsType,
    string? MgmtStatus,
    string? Organization,
    string? GroupCompany,
    string? Division,
    string? DivisionHead,
    string? AccrualCenter,
    string? ObjectName,
    string? District,
    string? House,
    string? AdrN,
    string? RoomNo
);

public sealed record PeriodBalanceDto(
    Guid Id,
    Guid AccountId,
    DateOnly PeriodDate,
    decimal DebtStart,
    decimal Accrued,
    decimal Paid,
    decimal DebtEnd,
    int? MonthsInDebt,
    string? DebtCategory,
    string? DebtStructure,
    string? SrcFile,
    string? RoomNo
)
{
    public bool IsBalanced(decimal tolerance = 0.01m)
        => Math.Abs((DebtStart + Accrued - Paid) - DebtEnd) <= tolerance;
}

public sealed record CaseFileDto(
    Guid Id,
    Guid AccountId,
    DateTimeOffset CreatedAt,
    CaseStatus Status,
    DebtorType DebtorType,
    decimal DebtAmount,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    string ServiceKind,
    string MgmtStatusText,
    string EnrichmentFlagsJson
);

public sealed record ImportReportDto(
    int RowsRead,
    int RowsImported,
    int Errors,
    IReadOnlyList<string> Messages
);

public sealed record DocPackageResultDto(
    Guid CaseId,
    bool Success,
    string OutputFolder,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Warnings
);
