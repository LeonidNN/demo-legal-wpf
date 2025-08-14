using System.Linq;
using DemoLegal.Infrastructure.Database;
using DemoLegal.Infrastructure.Import;

if (args.Length >= 2 && args[0] == "import" && (args[1] == "--csv" || args[1] == "-c"))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Укажите путь к CSV: DemoLegal.Cli import --csv path/to/file.csv");
        return 2;
    }
    var path = args[2];
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"Файл не найден: {path}");
        return 3;
    }

    var svc = new CsvImportService();
    var res = await svc.ImportCsvAsync(path);
    Console.WriteLine($"OK. Read={res.RowsRead} Imported={res.RowsImported} BalanceMismatch={res.BalanceMismatches}");
    if (res.Warnings.Count > 0)
    {
        Console.WriteLine("Warnings:");
        foreach (var w in res.Warnings.Take(10))
            Console.WriteLine(" - " + w);
        if (res.Warnings.Count > 10) Console.WriteLine(" ...");
    }
    return 0;
}
else
{
    Console.WriteLine("DemoLegal.Cli usage:");
    Console.WriteLine("  import --csv <path>   Импорт CSV отчёта");
    // Инициализация БД по умолчанию
    var dbPath = DbBootstrap.EnsureDatabase();
    Console.WriteLine($"DB ready at: {dbPath}");
    return 0;
}
