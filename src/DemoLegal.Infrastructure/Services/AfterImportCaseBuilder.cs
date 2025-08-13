using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DemoLegal.Application.Abstractions;
using DemoLegal.Application.DTOs;
using DemoLegal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DemoLegal.Infrastructure.Services;

/// <summary>
/// Реализация оркестратора: находит последний период по каждому ЛС и вызывает ICaseService.
/// </summary>
public sealed class AfterImportCaseBuilder : IAfterImportCaseBuilder
{
    private readonly DemoContext _db;
    private readonly ICaseService _caseService;

    public AfterImportCaseBuilder(DemoContext db, ICaseService caseService)
    {
        _db = db;
        _caseService = caseService;
    }

    public async Task<int> BuildCasesForAllAccountsAsync()
    {
        var accountIds = await _db.PeriodBalances
            .GroupBy(p => p.AccountId)
            .Select(g => g.Key)
            .ToListAsync()
            .ConfigureAwait(false);

        return await BuildCasesForAccountsAsync(accountIds).ConfigureAwait(false);
    }

    public async Task<int> BuildCasesForAccountsAsync(IEnumerable<Guid> accountIds)
    {
        int processed = 0;

        foreach (var accId in accountIds)
        {
            var account = await _db.Accounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == accId).ConfigureAwait(false);
            if (account is null) continue;

            var lastPeriod = await _db.PeriodBalances.AsNoTracking()
                .Where(p => p.AccountId == accId)
                .OrderByDescending(p => p.PeriodDate)
                .FirstOrDefaultAsync().ConfigureAwait(false);
            if (lastPeriod is null) continue;

            // маппинг в DTO
            var accDto = new AccountDto(
                account.Id, account.Ls, account.LsCode, account.Fio, account.AddressRaw, account.AddressNormJson,
                account.PremisesType, account.LsStatus, account.LsCloseDate, account.LsType, account.MgmtStatus,
                account.Organization, account.GroupCompany, account.Division, account.DivisionHead,
                account.AccrualCenter, account.ObjectName, account.District, account.House, account.AdrN, account.RoomNo
            );

            var pbDto = new PeriodBalanceDto(
                lastPeriod.Id, lastPeriod.AccountId, lastPeriod.PeriodDate,
                lastPeriod.DebtStart, lastPeriod.Accrued, lastPeriod.Paid, lastPeriod.DebtEnd,
                lastPeriod.MonthsInDebt, lastPeriod.DebtCategory, lastPeriod.DebtStructure,
                lastPeriod.SrcFile, lastPeriod.RoomNo
            );

            await _caseService.UpsertCaseAsync(accDto, pbDto).ConfigureAwait(false);
            processed++;
        }

        return processed;
    }
}
