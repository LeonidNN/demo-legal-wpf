using Dapper;
using DemoLegal.Domain.Models;
using DemoLegal.Infrastructure.Database;

namespace DemoLegal.Infrastructure.Repositories;

public interface ICaseQueryRepository
{
    Task<IReadOnlyList<CaseFileSummary>> GetCandidatesAsync(int limit = 500);
}

public sealed class CaseQueryRepository : ICaseQueryRepository
{
    public async Task<IReadOnlyList<CaseFileSummary>> GetCandidatesAsync(int limit = 500)
    {
        using var db = SqliteConnectionFactory.Create();
        var sql = @"
SELECT
  cf.id            AS CaseId,
  a.ls             AS Ls,
  a.fio            AS Fio,
  COALESCE(a.address_norm, a.address_raw) AS Address,
  cf.debtor_type   AS DebtorType,
  cf.debt_amount   AS DebtAmount,
  cf.period_from   AS PeriodFrom,
  cf.period_to     AS PeriodTo,
  COALESCE(a.premises_type, '') AS PremisesType,
  cf.mgmt_status_text AS MgmtStatusText
FROM case_file cf
JOIN account a ON a.id = cf.account_id
WHERE cf.status = 'candidate'
ORDER BY cf.debt_amount DESC
LIMIT @limit;";

        var rows = await db.QueryAsync(sql, new { limit });
        var list = new List<CaseFileSummary>();
        foreach (var r in rows)
        {
            string fmt(string? ymd) {
                if (string.IsNullOrWhiteSpace(ymd)) return "";
                if (DateTime.TryParse(ymd, out var dt)) return $"{dt:MM.yyyy}";
                return ymd!;
            }
            var from = fmt((string)r.PeriodFrom);
            var to   = fmt((string)r.PeriodTo);
            list.Add(new CaseFileSummary {
                CaseId = (int)(long)r.CaseId,
                Ls = (string)r.Ls,
                Fio = r.Fio as string,
                Address = (string)r.Address,
                DebtorType = (string)r.DebtorType,
                DebtAmount = (decimal)r.DebtAmount,
                Period = string.IsNullOrEmpty(from) ? "" : (from == to ? from : $"{from}{to}"),
                PremisesType = (string)r.PremisesType,
                MgmtStatusText = (string)r.MgmtStatusText
            });
        }
        return list;
    }
}
