using Microsoft.EntityFrameworkCore;

namespace DemoLegal.Infrastructure.Persistence;

public static class SqliteFactory
{
    public static DbContextOptions<DemoContext> CreateOptions(string? dbPath = null)
    {
        var path = DbPathProvider.Normalize(dbPath);
        var builder = new DbContextOptionsBuilder<DemoContext>()
            .UseSqlite($"Data Source={path};Cache=Shared");
        return builder.Options;
    }
}
