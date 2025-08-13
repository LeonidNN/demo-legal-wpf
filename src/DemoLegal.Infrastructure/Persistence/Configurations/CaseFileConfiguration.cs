using DemoLegal.Domain;
using DemoLegal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DemoLegal.Infrastructure.Persistence.Configurations;

public sealed class CaseFileConfiguration : IEntityTypeConfiguration<CaseFile>
{
    public void Configure(EntityTypeBuilder<CaseFile> b)
    {
        b.ToTable("case_file");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .ValueGeneratedNever();

        b.Property(x => x.AccountId).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();

        // Храним enum как строку для читаемости
        b.Property(x => x.Status)
            .HasConversion<string>()
            .IsRequired();

        b.Property(x => x.DebtorType)
            .HasConversion<string>()
            .IsRequired();

        b.Property(x => x.DebtAmount).HasColumnType("NUMERIC");

        b.Property(x => x.PeriodFrom).IsRequired();
        b.Property(x => x.PeriodTo).IsRequired();

        b.Property(x => x.ServiceKind).HasMaxLength(200).IsRequired();
        b.Property(x => x.MgmtStatusText).HasMaxLength(1000).IsRequired();

        b.Property(x => x.EnrichmentFlagsJson).HasColumnType("TEXT").IsRequired();

        b.HasIndex(x => new { x.AccountId, x.Status }).HasDatabaseName("IX_case_file_account_status");
    }
}
