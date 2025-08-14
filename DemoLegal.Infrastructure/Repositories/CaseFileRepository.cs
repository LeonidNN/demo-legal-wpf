using DemoLegal.Domain.Models;
using Dapper;
using DemoLegal.Infrastructure.Database;

namespace DemoLegal.Infrastructure.Repositories;

public interface ICaseFileRepository
{
    Task UpsertAsync(CaseFile cf);
}

public sealed class CaseFileRepository : ICaseFileRepository
{
    public async Task UpsertAsync(CaseFile cf)
    {
        using var db = SqliteConnectionFactory.Create();
        var existId = await db.ExecuteScalarAsync<int?>(
            "SELECT id FROM case_file WHERE account_id=@aid",
            new { aid = cf.AccountId });

        var payload = new {
            cf.AccountId,
            CreatedAt = cf.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            cf.Status, cf.DebtorType, cf.DebtAmount,
            PeriodFrom = cf.PeriodFrom.ToString("yyyy-MM-01"),
            PeriodTo = cf.PeriodTo.ToString("yyyy-MM-01"),
            cf.ServiceKind, cf.MgmtStatusText, cf.EnrichmentFlagsJson
        };

        if (existId is int id)
        {
            var sqlUpd = @"UPDATE case_file SET
 created_at=@CreatedAt, status=@Status, debtor_type=@DebtorType, debt_amount=@DebtAmount,
 period_from=@PeriodFrom, period_to=@PeriodTo, service_kind=@ServiceKind,
 mgmt_status_text=@MgmtStatusText, enrichment_flags=@EnrichmentFlagsJson
 WHERE id=@Id;";
            await db.ExecuteAsync(sqlUpd, new { Id = id, payload.CreatedAt, payload.Status, payload.DebtorType,
                payload.DebtAmount, payload.PeriodFrom, payload.PeriodTo, payload.ServiceKind,
                payload.MgmtStatusText, payload.EnrichmentFlagsJson });
        }
        else
        {
            var sqlIns = @"INSERT INTO case_file
(account_id, created_at, status, debtor_type, debt_amount, period_from, period_to, service_kind, mgmt_status_text, enrichment_flags)
VALUES (@AccountId, @CreatedAt, @Status, @DebtorType, @DebtAmount, @PeriodFrom, @PeriodTo, @ServiceKind, @MgmtStatusText, @EnrichmentFlagsJson);";
            await db.ExecuteAsync(sqlIns, payload);
        }
    }
}
