using DemoLegal.Domain.Models;
using Dapper;
using DemoLegal.Infrastructure.Database;

namespace DemoLegal.Infrastructure.Repositories;

public interface IPeriodBalanceRepository
{
    Task UpsertAsync(PeriodBalance pb);
    Task<PeriodBalance?> GetByAccountAndPeriodAsync(int accountId, DateOnly period);
    Task<PeriodBalance?> GetLatestByAccountAsync(int accountId);
}

public sealed class PeriodBalanceRepository : IPeriodBalanceRepository
{
    public async Task UpsertAsync(PeriodBalance pb)
    {
        using var db = SqliteConnectionFactory.Create();
        var exist = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM period_balance WHERE account_id=@aid AND period_date=@pd",
            new { aid = pb.AccountId, pd = pb.PeriodDate.ToString("yyyy-MM-01") }
        );
        if (exist > 0)
        {
            var sqlUpd = @"UPDATE period_balance SET
 debt_start=@DebtStart, accrued=@Accrued, paid=@Paid, debt_end=@DebtEnd,
 months_in_debt=@MonthsInDebt, debt_category=@DebtCategory, debt_structure=@DebtStructure,
 src_file=@SrcFile, room_no=@RoomNo
 WHERE account_id=@AccountId AND period_date=@Period;";
            await db.ExecuteAsync(sqlUpd, new {
                pb.DebtStart, pb.Accrued, pb.Paid, pb.DebtEnd, pb.MonthsInDebt,
                pb.DebtCategory, pb.DebtStructure, pb.SrcFile, pb.RoomNo,
                pb.AccountId, Period = pb.PeriodDate.ToString("yyyy-MM-01")
            });
        }
        else
        {
            var sqlIns = @"INSERT INTO period_balance
(account_id, period_date, debt_start, accrued, paid, debt_end, months_in_debt, debt_category, debt_structure, src_file, room_no)
VALUES (@AccountId, @Period, @DebtStart, @Accrued, @Paid, @DebtEnd, @MonthsInDebt, @DebtCategory, @DebtStructure, @SrcFile, @RoomNo);";
            await db.ExecuteAsync(sqlIns, new {
                pb.AccountId, Period = pb.PeriodDate.ToString("yyyy-MM-01"),
                pb.DebtStart, pb.Accrued, pb.Paid, pb.DebtEnd, pb.MonthsInDebt,
                pb.DebtCategory, pb.DebtStructure, pb.SrcFile, pb.RoomNo
            });
        }
    }

    public async Task<PeriodBalance?> GetByAccountAndPeriodAsync(int accountId, DateOnly period)
    {
        using var db = SqliteConnectionFactory.Create();
        return await db.QueryFirstOrDefaultAsync<PeriodBalance>(
            "SELECT * FROM period_balance WHERE account_id=@aid AND period_date=@pd",
            new { aid = accountId, pd = period.ToString("yyyy-MM-01") });
    }

    public async Task<PeriodBalance?> GetLatestByAccountAsync(int accountId)
    {
        using var db = SqliteConnectionFactory.Create();
        return await db.QueryFirstOrDefaultAsync<PeriodBalance>(
            "SELECT * FROM period_balance WHERE account_id=@aid ORDER BY period_date DESC LIMIT 1",
            new { aid = accountId });
    }
}
