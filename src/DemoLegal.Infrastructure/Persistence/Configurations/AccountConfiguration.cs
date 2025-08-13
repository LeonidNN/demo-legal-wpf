using DemoLegal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DemoLegal.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> b)
    {
        b.ToTable("account");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .ValueGeneratedNever();

        b.Property(x => x.Ls)
            .IsRequired();

        b.Property(x => x.LsCode);
        b.Property(x => x.Fio);
        b.Property(x => x.AddressRaw).IsRequired();
        b.Property(x => x.AddressNormJson);

        b.Property(x => x.PremisesType);
        b.Property(x => x.LsStatus);
        b.Property(x => x.LsCloseDate); // EF Core 8 поддерживает DateOnly для SQLite
        b.Property(x => x.LsType);
        b.Property(x => x.MgmtStatus);

        b.Property(x => x.Organization);
        b.Property(x => x.GroupCompany);
        b.Property(x => x.Division);
        b.Property(x => x.DivisionHead);
        b.Property(x => x.AccrualCenter);
        b.Property(x => x.ObjectName);
        b.Property(x => x.District);
        b.Property(x => x.House);
        b.Property(x => x.AdrN);
        b.Property(x => x.RoomNo);

        // Индексы для быстрого поиска
        b.HasIndex(x => x.Ls);
        b.HasIndex(x => new { x.AccrualCenter, x.Ls }).HasDatabaseName("IX_account_center_ls");
    }
}
