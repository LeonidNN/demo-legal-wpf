using Dapper;
using DemoLegal.Domain.Models;
using DemoLegal.Infrastructure.Database;

namespace DemoLegal.Infrastructure.Repositories;

public interface IAccountRepository
{
    Task<int> GetOrCreateAsync(Account acc);
    Task<int?> TryGetIdByLsAsync(string ls);
}

public sealed class AccountRepository : IAccountRepository
{
    public async Task<int?> TryGetIdByLsAsync(string ls)
    {
        if (string.IsNullOrWhiteSpace(ls)) return null;
        using var db = SqliteConnectionFactory.Create();
        var id = await db.ExecuteScalarAsync<int?>(
            "SELECT id FROM account WHERE ls=@ls LIMIT 1;",
            new { ls = ls.Trim() });
        return id;
    }

    public async Task<int> GetOrCreateAsync(Account acc)
    {
        if (acc is null) throw new ArgumentNullException(nameof(acc));
        if (string.IsNullOrWhiteSpace(acc.Ls))
            throw new InvalidOperationException("Поле ЛС пустое — запись лицевого счёта невозможна.");

        using var db = SqliteConnectionFactory.Create();

        var existingId = await db.ExecuteScalarAsync<int?>(
            "SELECT id FROM account WHERE ls=@ls LIMIT 1;",
            new { ls = acc.Ls.Trim() });
        if (existingId is int foundId)
            return foundId;

        var sqlIns = @"
INSERT INTO account
(ls, ls_code, fio, address_raw, address_norm, premises_type, ls_status, ls_close_date, ls_type,
 mgmt_status, organization, group_company, division, division_head, accrual_center, object_name, district, house, adrN)
VALUES
(@ls, @ls_code, @fio, @address_raw, @address_norm, @premises_type, @ls_status, @ls_close_date, @ls_type,
 @mgmt_status, @organization, @group_company, @division, @division_head, @accrual_center, @object_name, @district, @house, @adrN);
SELECT last_insert_rowid();";

        var newId = await db.ExecuteScalarAsync<long>(sqlIns, new
        {
            ls = acc.Ls.Trim(),
            ls_code = acc.LsCode,
            fio = acc.Fio,
            address_raw = acc.AddressRaw,
            address_norm = acc.AddressNorm,
            premises_type = acc.PremisesType,
            ls_status = acc.LsStatus,
            ls_close_date = acc.LsCloseDate?.ToString("yyyy-MM-dd"),
            ls_type = acc.LsType,
            mgmt_status = acc.MgmtStatus,
            organization = acc.Organization,
            group_company = acc.GroupCompany,
            division = acc.Division,
            division_head = acc.DivisionHead,
            accrual_center = acc.AccrualCenter,
            object_name = acc.ObjectName,
            district = acc.District,
            house = acc.House,
            adrN = acc.AdrN
        });

        return (int)newId;
    }
}
