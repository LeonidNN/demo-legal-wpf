using System.Text;

namespace DemoLegal.Infrastructure.Import;

public sealed class ImportSummary
{
    public int RowsRead { get; set; }
    public int RowsImported { get; set; }
    public int AccountsCreated { get; set; }
    public int BalanceMismatches { get; set; }
    public List<string> Warnings { get; } = new();

    public string BuildReportString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Импорт отчёта — {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
        sb.AppendLine($"Прочитано строк: {RowsRead}");
        sb.AppendLine($"Импортировано: {RowsImported}");
        sb.AppendLine($"Несхождения баланса: {BalanceMismatches}");
        if (Warnings.Count > 0)
        {
            sb.AppendLine("Предупреждения (первые 200):");
            foreach (var w in Warnings.Take(200)) sb.AppendLine(" - " + w);
        }
        return sb.ToString();
    }
}
