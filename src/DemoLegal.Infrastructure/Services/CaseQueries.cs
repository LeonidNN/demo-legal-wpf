using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DemoLegal.Application.Abstractions;
using DemoLegal.Application.DTOs;
using DemoLegal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DemoLegal.Infrastructure.Services;

public sealed class CaseQueries : ICaseQueries
{
    private readonly DemoContext _db;

    public CaseQueries(DemoContext db) => _db = db;

    public async Task<IReadOnlyList<CaseFileDto>> GetRecentCasesAsync(int take = 100)
    {
        var q = _db.CaseFiles.AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Take(take);

        var list = await q.ToListAsync().ConfigureAwait(false);
        return list.ConvertAll(c => new CaseFileDto(
            c.Id, c.AccountId, c.CreatedAt, c.Status, c.DebtorType,
            c.DebtAmount, c.PeriodFrom, c.PeriodTo, c.ServiceKind, c.MgmtStatusText, c.EnrichmentFlagsJson));
    }

    public async Task<CaseFileDto?> GetByIdAsync(Guid caseId)
    {
        var c = await _db.CaseFiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == caseId).ConfigureAwait(false);
        if (c is null) return null;
        return new CaseFileDto(
            c.Id, c.AccountId, c.CreatedAt, c.Status, c.DebtorType,
            c.DebtAmount, c.PeriodFrom, c.PeriodTo, c.ServiceKind, c.MgmtStatusText, c.EnrichmentFlagsJson);
    }
}
