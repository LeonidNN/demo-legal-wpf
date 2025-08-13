using System.Threading.Tasks;

namespace DemoLegal.Infrastructure.Persistence;

public static class DbInitializer
{
    /// <summary>
    /// Для MVP используем EnsureCreated (без миграций). Позже можно перейти на Migrations.
    /// </summary>
    public static async Task EnsureCreatedAsync(DemoContext ctx)
    {
        await ctx.Database.EnsureCreatedAsync().ConfigureAwait(false);
    }
}
