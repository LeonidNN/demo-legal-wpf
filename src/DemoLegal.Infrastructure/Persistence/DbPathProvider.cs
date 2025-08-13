using System;
using System.IO;

namespace DemoLegal.Infrastructure.Persistence;

public static class DbPathProvider
{
    public static string GetDefaultPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "DemoLegal");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "data.sqlite");
    }

    public static string Normalize(string? pathOrNull)
        => string.IsNullOrWhiteSpace(pathOrNull) ? GetDefaultPath() : Environment.ExpandEnvironmentVariables(pathOrNull);
}
