using DemoLegal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DemoLegal.Infrastructure.Persistence.Configurations;

public sealed class PeriodBalanceConfiguration : IEntityTypeConfiguration<PeriodBalance>
{
    public void Configure(EntityTypeBuilder<PeriodBalance> b)
    {
        b.ToTable("period_balance");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .ValueGeneratedNever();

        b.Property(x => x.AccountId)
            .IsRequired();

        b.Property(x => x.PeriodDate)
            .IsRequired();

        b.Property(x => x.DebtStart).HasColumnType("NUMERIC");
        b.Property(x => x.Accrued).HasColumnType("NUMERIC");
        b.Property(x => x.Paid).HasColumnType("NUMERIC");
        b.Property(x => x.DebtEnd).HasColumnType("NUMERIC");

        b.Property(x => x.MonthsInDebt);
        b.Property(x => x.DebtCategory);
        b.Property(x => x.DebtStructure);
        b.Property(x => x.SrcFile);
        b.Property(x => x.RoomNo);

        // Отношение: многие PeriodBalance к одному Account (по AccountId)
        b.HasIndex(x => new { x.AccountId, x.PeriodDate }).HasDatabaseName("IX_period_balance_account_period");
    }
}
