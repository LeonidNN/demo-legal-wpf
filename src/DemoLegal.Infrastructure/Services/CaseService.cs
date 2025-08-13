using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DemoLegal.Application.Abstractions;
using DemoLegal.Application.DTOs;
using DemoLegal.Domain;
using DemoLegal.Domain.Entities;
using DemoLegal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DemoLegal.Infrastructure.Services;

public sealed class CaseService : ICaseService
{
    private readonly DemoContext _db;

    public CaseService(DemoContext db) => _db = db;

    public async Task<CaseFileDto> UpsertCaseAsync(AccountDto accountDto, PeriodBalanceDto lastPeriodDto)
    {
        // загрузим сущность Account из БД
        var account = await _db.Accounts.FirstAsync(a => a.Id == accountDto.Id).ConfigureAwait(false);

        var debtorType = account.DetermineDebtorType();

        // Период
        var periodTo = lastPeriodDto.PeriodDate;
        var periodFrom = ComputePeriodFrom(periodTo, lastPeriodDto.MonthsInDebt);

        // Текст статуса управления
        var mgmtText = account.BuildMgmtStatusText(periodTo);

        // Флаги добора
        var flags = new EnrichmentFlags
        {
            NeedInn = debtorType == DebtorType.Company,
            NeedBirthDate = debtorType == DebtorType.Person,
            NeedBirthPlace = debtorType == DebtorType.Person,
            NeedPeriodRefine = !lastPeriodDto.MonthsInDebt.HasValue
        };

        // Upsert CaseFile (уникальность по AccountId)
        var caseFile = await _db.CaseFiles.FirstOrDefaultAsync(c => c.AccountId == account.Id).ConfigureAwait(false);
        if (caseFile is null)
        {
            caseFile = new CaseFile
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Status = CaseStatus.Candidate,
                DebtorType = debtorType,
                DebtAmount = lastPeriodDto.DebtEnd,
                PeriodFrom = periodFrom,
                PeriodTo = periodTo,
                ServiceKind = "ЖКУ (обобщ.)",
                MgmtStatusText = mgmtText
            };
            caseFile.SetFlags(flags);
            _db.CaseFiles.Add(caseFile);
        }
        else
        {
            caseFile.DebtorType = debtorType;
            caseFile.DebtAmount = lastPeriodDto.DebtEnd;
            caseFile.PeriodFrom = periodFrom;
            caseFile.PeriodTo = periodTo;
            caseFile.ServiceKind = "ЖКУ (обобщ.)";
            caseFile.MgmtStatusText = mgmtText;
            caseFile.SetFlags(flags);
            _db.Entry(caseFile).State = EntityState.Modified;
        }

        await _db.SaveChangesAsync().ConfigureAwait(false);

        return new CaseFileDto(
            caseFile.Id, caseFile.AccountId, caseFile.CreatedAt, caseFile.Status, caseFile.DebtorType,
            caseFile.DebtAmount, caseFile.PeriodFrom, caseFile.PeriodTo,
            caseFile.ServiceKind, caseFile.MgmtStatusText, caseFile.EnrichmentFlagsJson);
    }

    private static DateOnly ComputePeriodFrom(DateOnly periodTo, int? monthsInDebt)
    {
        if (monthsInDebt is null or <= 0) return periodTo;
        // начало = конец - N + 1 месяц
        var months = monthsInDebt.Value - 1;
        var dt = new DateTime(periodTo.Year, periodTo.Month, 1).AddMonths(-months);
        return new DateOnly(dt.Year, dt.Month, 1);
    }
}
