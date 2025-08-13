using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DemoLegal.Infrastructure.Persistence;

/// <summary>
/// Для инструментов (если понадобятся миграции).
/// </summary>
public sealed class DesignTimeDemoContextFactory : IDesignTimeDbContextFactory<DemoContext>
{
    public DemoContext CreateDbContext(string[] args)
    {
        var options = SqliteFactory.CreateOptions(null);
        return new DemoContext(options);
    }
}
