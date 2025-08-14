using DemoLegal.Domain.Models;
using Dapper;
using DemoLegal.Infrastructure.Database;

namespace DemoLegal.Infrastructure.Repositories;

public interface IAccountRepository
{
    Task<int> GetOrCreateAsync(Account acc);
    Task<Account?> GetByLsAsync(string ls);
}

public sealed class AccountRepository : IAccountRepository
{
    public async Task<int> GetOrCreateAsync(Account acc)
    {
        using var db = SqliteConnectionFactory.Create();
        var id = await db.ExecuteScalarAsync<int?>(
            "SELECT id FROM account WHERE ls = @Ls",
            new { acc.Ls }
        );
        if (id is int existingId) return existingId;

        var sql = @"INSERT INTO account
(ls, ls_code, fio, address_raw, address_norm, premises_type, ls_status, ls_close_date, ls_type, mgmt_status,
 organization, group_company, division, division_head, accrual_center, object_name, district, house, adrN)
VALUES
(@Ls, @LsCode, @Fio, @AddressRaw, @AddressNorm, @PremisesType, @LsStatus, @LsCloseDate, @LsType, @MgmtStatus,
 @Organization, @GroupCompany, @Division, @DivisionHead, @AccrualCenter, @ObjectName, @District, @House, @AdrN);
SELECT last_insert_rowid();";

        var newId = await db.ExecuteScalarAsync<long>(sql, new {
            acc.Ls, acc.LsCode, acc.Fio, acc.AddressRaw, acc.AddressNorm, acc.PremisesType, acc.LsStatus,
            LsCloseDate = acc.LsCloseDate?.ToString("yyyy-MM-dd"),
            acc.LsType, acc.MgmtStatus, acc.Organization, acc.GroupCompany, acc.Division, acc.DivisionHead,
            acc.AccrualCenter, acc.ObjectName, acc.District, acc.House, acc.AdrN
        });
        return (int)newId;
    }

    public async Task<Account?> GetByLsAsync(string ls)
    {
        using var db = SqliteConnectionFactory.Create();
        return await db.QueryFirstOrDefaultAsync<Account>(
            "SELECT * FROM account WHERE ls = @ls", new { ls });
    }
}
