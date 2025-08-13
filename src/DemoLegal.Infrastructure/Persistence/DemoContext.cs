using DemoLegal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DemoLegal.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext: маппит доменные сущности на SQLite.
/// </summary>
public sealed class DemoContext : DbContext
{
    public DemoContext(DbContextOptions<DemoContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<PeriodBalance> PeriodBalances => Set<PeriodBalance>();
    public DbSet<CaseFile> CaseFiles => Set<CaseFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DemoContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
