using Microsoft.Data.Sqlite;

namespace DemoLegal.Infrastructure.Database;

public static class DbBootstrap
{
    public static string EnsureDatabase(string? dbPath = null)
    {
        var path = string.IsNullOrWhiteSpace(dbPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DemoLegal", "data.sqlite")
            : dbPath;

        var dir = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();

        var schemaSql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Database", "schema.sql"));
        using var cmd = connection.CreateCommand();
        cmd.CommandText = schemaSql;
        cmd.ExecuteNonQuery();

        return path;
    }
}
