using Microsoft.Data.Sqlite;

namespace DemoLegal.Infrastructure.Database;

public static class SqliteConnectionFactory
{
    private static string? _dbPath;

    public static void Configure(string? dbPath = null)
    {
        _dbPath = DbBootstrap.EnsureDatabase(dbPath);
    }

    public static SqliteConnection Create()
    {
        // EnsureDatabase идемпотентен — безопасно
        var path = _dbPath ?? DbBootstrap.EnsureDatabase();
        var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        return conn;
    }
}
